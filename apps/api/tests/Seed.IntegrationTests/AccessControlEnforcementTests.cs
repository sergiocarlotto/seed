using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Seed.Application.AccessControl;
using Seed.Domain.AccessControl;
using Seed.Infrastructure.Identity;
using Seed.Infrastructure.Persistence;

namespace Seed.IntegrationTests;

// Enforcement: resolução de permissão efetiva e proteção do endpoint /permissions.
public class AccessControlEnforcementTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    // Cria um perfil ativo na org com as permissões dadas e vincula ao usuário.
    private static async Task GiveProfileAsync(
        SeedDbContext db, Guid orgId, Guid userId, string profileName,
        ProfileStatus status, params string[] permissionKeys)
    {
        var now = DateTime.UtcNow;
        var profile = new Profile
        {
            OrganizationId = orgId, Name = profileName, Status = status,
            CreatedAt = now, UpdatedAt = now,
        };
        db.Profiles.Add(profile);
        foreach (var key in permissionKeys)
            db.ProfilePermissions.Add(new ProfilePermission { ProfileId = profile.Id, PermissionKey = key });
        db.UserProfiles.Add(new UserProfile { UserId = userId, ProfileId = profile.Id });
        await db.SaveChangesAsync();
    }

    private static async Task<(Guid userId, Guid orgId)> CreateUserAsync(
        ApiFactory factory, string email)
    {
        var orgId = await factory.GetDemoOrganizationIdAsync();
        await factory.CreateUserAsync(email, "Passw0rd!", orgId, Seed.Domain.Organizations.OrganizationRole.Member);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
        var id = await db.Users.Where(u => u.Email == email).Select(u => u.Id).FirstAsync();
        return (id, orgId);
    }

    [Fact]
    public async Task Effective_permissions_are_union_and_exclude_archived()
    {
        var (userId, orgId) = await CreateUserAsync(factory, "union@demo.local");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
        await GiveProfileAsync(db, orgId, userId, "Ativo A", ProfileStatus.Active,
            AccessControlPermissions.ProfilesManage);
        await GiveProfileAsync(db, orgId, userId, "Ativo B", ProfileStatus.Active,
            AccessControlPermissions.UsersManage);
        await GiveProfileAsync(db, orgId, userId, "Arquivado", ProfileStatus.Archived,
            AccessControlPermissions.ProfilesAssign);

        // Resolve as permissões efetivas simulando o usuário logado.
        var resolver = new Seed.Infrastructure.AccessControl.EffectivePermissionsService(
            db, new FixedCurrentUser(userId));
        var perms = await resolver.ForCurrentUserAsync(default);

        Assert.Contains(AccessControlPermissions.ProfilesManage, perms);
        Assert.Contains(AccessControlPermissions.UsersManage, perms);
        Assert.DoesNotContain(AccessControlPermissions.ProfilesAssign, perms); // perfil arquivado
    }

    [Fact]
    public async Task Owner_gets_all_active_permissions()
    {
        var (userId, _) = await CreateUserAsync(factory, "owner@demo.local");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        user.IsOwner = true;
        await db.SaveChangesAsync();

        var resolver = new Seed.Infrastructure.AccessControl.EffectivePermissionsService(
            db, new FixedCurrentUser(userId));
        var perms = await resolver.ForCurrentUserAsync(default);

        // Owner recebe todas as permissões ativas do catálogo, sem perfil algum.
        Assert.Contains(AccessControlPermissions.ProfilesManage, perms);
        Assert.Contains(AccessControlPermissions.ProfilesAssign, perms);
        Assert.Contains(AccessControlPermissions.UsersManage, perms);
    }

    [Fact]
    public async Task Get_permissions_requires_authentication()
    {
        var client = factory.CreateClient();
        var resp = await client.GetAsync("/permissions");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Get_permissions_forbidden_without_permission()
    {
        await CreateUserAsync(factory, "noperm@demo.local");
        var client = await factory.CreateLoggedInClientAsync("noperm@demo.local", "Passw0rd!");

        var resp = await client.GetAsync("/permissions");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Get_permissions_ok_with_profiles_manage()
    {
        var (userId, orgId) = await CreateUserAsync(factory, "canlist@demo.local");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
            await GiveProfileAsync(db, orgId, userId, "Gestor de Perfis", ProfileStatus.Active,
                AccessControlPermissions.ProfilesManage);
        }

        var client = await factory.CreateLoggedInClientAsync("canlist@demo.local", "Passw0rd!");
        var resp = await client.GetAsync("/permissions");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var groups = await resp.Content.ReadFromJsonAsync<List<PermissionGroupDto>>();
        Assert.NotNull(groups);
        Assert.Contains(groups!, g => g.Permissions.Any(p => p.Key == AccessControlPermissions.ProfilesManage));
    }

    [Fact]
    public async Task Get_permissions_ok_for_owner_without_profile()
    {
        var (userId, _) = await CreateUserAsync(factory, "ownerlist@demo.local");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
            var user = await db.Users.FirstAsync(u => u.Id == userId);
            user.IsOwner = true;
            await db.SaveChangesAsync();
        }

        var client = await factory.CreateLoggedInClientAsync("ownerlist@demo.local", "Passw0rd!");
        var resp = await client.GetAsync("/permissions");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    // ICurrentUser fixo para testar o serviço fora do pipeline HTTP.
    private sealed class FixedCurrentUser(Guid id) : Seed.Application.Abstractions.ICurrentUser
    {
        public Guid? UserId => id;
        public bool IsAuthenticated => true;
    }
}
