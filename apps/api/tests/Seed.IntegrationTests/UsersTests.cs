using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Seed.Application.AccessControl;
using Seed.Domain.AccessControl;
using Seed.Domain.Organizations;
using Seed.Infrastructure.AccessControl;
using Seed.Infrastructure.Persistence;

namespace Seed.IntegrationTests;

// Gestão de usuários (users.manage), atribuição de perfis (profiles.assign),
// desativação com bloqueio imediato, postura B anti-escalada, owner read-only e
// auditoria old/new.
public class UsersTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    // Cria um usuário na org Demo e devolve seu id. Sem perfil (zero permissão).
    private async Task<Guid> CreateMemberAsync(string email)
    {
        var orgId = await factory.GetDemoOrganizationIdAsync();
        await factory.CreateUserAsync(email, "Passw0rd!", orgId, OrganizationRole.Member);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
        return await db.Users.Where(u => u.Email == email).Select(u => u.Id).FirstAsync();
    }

    // Vincula ao usuário um perfil ativo com as permissões dadas (nome único).
    private async Task GiveProfileAsync(Guid userId, string profileName, params string[] permissionKeys)
    {
        var orgId = await factory.GetDemoOrganizationIdAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
        var now = DateTime.UtcNow;
        var profile = new Profile
        {
            OrganizationId = orgId, Name = profileName, Status = ProfileStatus.Active,
            CreatedAt = now, UpdatedAt = now,
        };
        db.Profiles.Add(profile);
        foreach (var key in permissionKeys)
            db.ProfilePermissions.Add(new ProfilePermission { ProfileId = profile.Id, PermissionKey = key });
        db.UserProfiles.Add(new UserProfile { UserId = userId, ProfileId = profile.Id });
        await db.SaveChangesAsync();
    }

    // Client logado como um gestor (perfil ativo concedendo as permissões dadas).
    private async Task<HttpClient> ClientWithAsync(string email, params string[] permissionKeys)
    {
        var userId = await CreateMemberAsync(email);
        await GiveProfileAsync(userId, $"Perfil {email}", permissionKeys);
        return await factory.CreateLoggedInClientAsync(email, "Passw0rd!");
    }

    private async Task<Guid> DemoAdminIdAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
        return await db.Users.Where(u => u.Email == ApiFactory.AdminEmail).Select(u => u.Id).FirstAsync();
    }

    private async Task<Guid> SystemProfileIdAsync()
    {
        var orgId = await factory.GetDemoOrganizationIdAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
        return await db.Profiles.Where(p => p.OrganizationId == orgId && p.IsSystem).Select(p => p.Id).FirstAsync();
    }

    [Fact]
    public async Task List_requires_users_manage()
    {
        await CreateMemberAsync("users.noperm@demo.local");
        var client = await factory.CreateLoggedInClientAsync("users.noperm@demo.local", "Passw0rd!");
        var resp = await client.GetAsync("/users");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task List_shows_members_with_profiles_and_companies()
    {
        var client = await ClientWithAsync("users.list@demo.local", AccessControlPermissions.UsersManage);
        var list = await client.GetFromJsonAsync<List<UserDto>>("/users");
        Assert.NotNull(list);

        // O admin semeado (owner) aparece marcado e com a empresa Demo acessível.
        var owner = list!.FirstOrDefault(u => u.Email == ApiFactory.AdminEmail);
        Assert.NotNull(owner);
        Assert.True(owner!.IsOwner);
        Assert.Contains(owner.Companies, c => c.Name == ApiFactory.DemoCompanyName);

        // O próprio gestor aparece com o perfil que recebeu.
        var self = list.FirstOrDefault(u => u.Email == "users.list@demo.local");
        Assert.NotNull(self);
        Assert.Contains(self!.Profiles, p => p.Name == "Perfil users.list@demo.local");
    }

    [Fact]
    public async Task Get_cross_tenant_user_is_404()
    {
        var client = await ClientWithAsync("users.get@demo.local", AccessControlPermissions.UsersManage);
        var other = await factory.CreateSecondTenantAsync(
            orgName: "Org X", companyName: "Emp X", userEmail: "x@x.local", userPassword: "Xxxx123!");
        // O usuário da outra org não é visível → 404 (não vaza existência).
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
        var otherUserId = await db.Users.Where(u => u.Email == "x@x.local").Select(u => u.Id).FirstAsync();
        var resp = await client.GetAsync($"/users/{otherUserId}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Deactivate_blocks_access_immediately()
    {
        // Gestor com users.manage é desativado pelo owner e perde acesso no
        // próximo request (sem cache entre requests).
        var targetId = await CreateMemberAsync("users.deact@demo.local");
        await GiveProfileAsync(targetId, "Perfil deact", AccessControlPermissions.UsersManage);
        var targetClient = await factory.CreateLoggedInClientAsync("users.deact@demo.local", "Passw0rd!");
        Assert.Equal(HttpStatusCode.OK, (await targetClient.GetAsync("/users")).StatusCode);

        var owner = await factory.CreateAdminClientAsync();
        var deact = await owner.PatchAsJsonAsync($"/users/{targetId}/status", new { active = false });
        Assert.Equal(HttpStatusCode.OK, deact.StatusCode);
        var dto = await deact.Content.ReadFromJsonAsync<UserDto>();
        Assert.Equal(UserStatus.Inactive.ToString(), dto!.Status);

        // Sessão ainda válida (cookie), mas permissão efetiva agora é vazia → 403.
        Assert.Equal(HttpStatusCode.Forbidden, (await targetClient.GetAsync("/users")).StatusCode);

        // E não consegue logar de novo.
        var relog = factory.CreateClient();
        var login = await relog.PostAsJsonAsync("/auth/login", new { email = "users.deact@demo.local", password = "Passw0rd!" });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);

        // Auditoria old/new do status.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
        var tid = targetId.ToString();
        Assert.True(await db.AuditEvents.AnyAsync(a =>
            a.Action == "access_control.user.status_changed" && a.EntityId == tid && a.ActorUserId != null));
    }

    [Fact]
    public async Task Cannot_deactivate_owner()
    {
        var owner = await factory.CreateAdminClientAsync();
        var ownerId = await DemoAdminIdAsync();
        var resp = await owner.PatchAsJsonAsync($"/users/{ownerId}/status", new { active = false });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Assign_profiles_sets_set_and_emits_audit()
    {
        // Um gestor com profiles.assign define os perfis de um membro.
        var manager = await ClientWithAsync("users.assign@demo.local", AccessControlPermissions.ProfilesAssign);
        var targetId = await CreateMemberAsync("users.target@demo.local");

        // Cria dois perfis comuns na org para atribuir.
        Guid p1, p2;
        var orgId = await factory.GetDemoOrganizationIdAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
            var now = DateTime.UtcNow;
            var a = new Profile { OrganizationId = orgId, Name = "Comum A", Status = ProfileStatus.Active, CreatedAt = now, UpdatedAt = now };
            var b = new Profile { OrganizationId = orgId, Name = "Comum B", Status = ProfileStatus.Active, CreatedAt = now, UpdatedAt = now };
            db.Profiles.AddRange(a, b);
            await db.SaveChangesAsync();
            p1 = a.Id; p2 = b.Id;
        }

        var put = await manager.PutAsJsonAsync($"/users/{targetId}/profiles", new { profileIds = new[] { p1, p2 } });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var dto = await put.Content.ReadFromJsonAsync<UserDto>();
        Assert.Equal(2, dto!.Profiles.Count);

        // Redefinir para apenas p1 remove p2 (operação de conjunto).
        var put2 = await manager.PutAsJsonAsync($"/users/{targetId}/profiles", new { profileIds = new[] { p1 } });
        var dto2 = await put2.Content.ReadFromJsonAsync<UserDto>();
        Assert.Single(dto2!.Profiles);
        Assert.Equal(p1, dto2.Profiles[0].Id);

        using var scope2 = factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<SeedDbContext>();
        var tid = targetId.ToString();
        Assert.True(await db2.AuditEvents.AnyAsync(a => a.Action == "access_control.user.profile_assigned" && a.EntityId == tid));
        Assert.True(await db2.AuditEvents.AnyAsync(a => a.Action == "access_control.user.profile_removed" && a.EntityId == tid));
    }

    [Fact]
    public async Task Assign_cross_tenant_profile_is_404()
    {
        var manager = await ClientWithAsync("users.xassign@demo.local", AccessControlPermissions.ProfilesAssign);
        var targetId = await CreateMemberAsync("users.xtarget@demo.local");
        var other = await factory.CreateSecondTenantAsync(
            orgName: "Org Y", companyName: "Emp Y", userEmail: "y@y.local", userPassword: "Yyyy123!");

        // Cria um perfil na outra org e tenta atribuí-lo (deve dar 404).
        Guid foreignProfile;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
            var now = DateTime.UtcNow;
            var p = new Profile { OrganizationId = other.OrganizationId, Name = "Perfil Estrangeiro", Status = ProfileStatus.Active, CreatedAt = now, UpdatedAt = now };
            db.Profiles.Add(p);
            await db.SaveChangesAsync();
            foreignProfile = p.Id;
        }

        var resp = await manager.PutAsJsonAsync($"/users/{targetId}/profiles", new { profileIds = new[] { foreignProfile } });
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Non_owner_cannot_assign_system_profile_but_owner_can()
    {
        var manager = await ClientWithAsync("users.escalate@demo.local", AccessControlPermissions.ProfilesAssign);
        var targetId = await CreateMemberAsync("users.escalate.target@demo.local");
        var systemId = await SystemProfileIdAsync();

        // Postura B: não-owner com profiles.assign não atribui o "Administrador".
        var denied = await manager.PutAsJsonAsync($"/users/{targetId}/profiles", new { profileIds = new[] { systemId } });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        // O owner consegue.
        var owner = await factory.CreateAdminClientAsync();
        var allowed = await owner.PutAsJsonAsync($"/users/{targetId}/profiles", new { profileIds = new[] { systemId } });
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    [Fact]
    public async Task Cannot_edit_owner_profiles()
    {
        var owner = await factory.CreateAdminClientAsync();
        var ownerId = await DemoAdminIdAsync();
        var resp = await owner.PutAsJsonAsync($"/users/{ownerId}/profiles", new { profileIds = Array.Empty<Guid>() });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Auth_me_exposes_permissions_and_companies()
    {
        var client = await factory.CreateAdminClientAsync();
        var me = await client.GetFromJsonAsync<MeResponse>("/auth/me");
        Assert.NotNull(me);
        Assert.True(me!.IsOwner);
        // Owner tem todas as permissões ativas do catálogo.
        Assert.Contains(AccessControlPermissions.ProfilesManage, me.Permissions);
        Assert.Contains(AccessControlPermissions.UsersManage, me.Permissions);
        Assert.Contains(me.Companies, c => c.Name == ApiFactory.DemoCompanyName);
    }

    // Espelho enxuto do payload de /auth/me para desserialização nos testes.
    private record MeResponse(
        bool IsOwner,
        List<string> Permissions,
        List<CompanyRef> Companies);
    private record CompanyRef(Guid Id, string Name);
}
