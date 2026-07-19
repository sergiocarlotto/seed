using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Seed.Application.AccessControl;
using Seed.Domain.AccessControl;
using Seed.Domain.Organizations;
using Seed.Infrastructure.Persistence;

namespace Seed.IntegrationTests;

// CRUD de perfis: invariantes (sistema imutável, nome único, permissão válida) e
// auditoria (old/new) na mesma transação.
public class ProfilesTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    // Cria um usuário com um perfil ativo concedendo profiles.manage e devolve um
    // client HTTP logado como ele.
    private async Task<HttpClient> ManagerClientAsync(string email)
    {
        var orgId = await factory.GetDemoOrganizationIdAsync();
        await factory.CreateUserAsync(email, "Passw0rd!", orgId, OrganizationRole.Member);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
            var userId = await db.Users.Where(u => u.Email == email).Select(u => u.Id).FirstAsync();
            var now = DateTime.UtcNow;
            var profile = new Profile
            {
                OrganizationId = orgId, Name = $"Gestor {email}", Status = ProfileStatus.Active,
                CreatedAt = now, UpdatedAt = now,
            };
            db.Profiles.Add(profile);
            db.ProfilePermissions.Add(new ProfilePermission
            {
                ProfileId = profile.Id, PermissionKey = AccessControlPermissions.ProfilesManage,
            });
            db.UserProfiles.Add(new UserProfile { UserId = userId, ProfileId = profile.Id });
            await db.SaveChangesAsync();
        }
        return await factory.CreateLoggedInClientAsync(email, "Passw0rd!");
    }

    [Fact]
    public async Task Create_then_get_and_list()
    {
        var client = await ManagerClientAsync("prof.create@demo.local");

        var create = await client.PostAsJsonAsync("/profiles", new
        {
            name = "Financeiro",
            description = "Acesso financeiro",
            permissionKeys = new[] { AccessControlPermissions.UsersManage },
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<ProfileDetailDto>();
        Assert.NotNull(created);
        Assert.Contains(AccessControlPermissions.UsersManage, created!.PermissionKeys);
        Assert.False(created.IsSystem);

        var detail = await client.GetFromJsonAsync<ProfileDetailDto>($"/profiles/{created.Id}");
        Assert.Equal("Financeiro", detail!.Name);

        var list = await client.GetFromJsonAsync<List<ProfileSummaryDto>>("/profiles");
        Assert.Contains(list!, p => p.Name == "Financeiro");
    }

    [Fact]
    public async Task Create_duplicate_name_is_rejected()
    {
        var client = await ManagerClientAsync("prof.dup@demo.local");
        await client.PostAsJsonAsync("/profiles", new { name = "Repetido", description = (string?)null, permissionKeys = new string[0] });

        var again = await client.PostAsJsonAsync("/profiles", new { name = "Repetido", description = (string?)null, permissionKeys = new string[0] });
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
    }

    [Fact]
    public async Task Create_with_invalid_permission_is_rejected()
    {
        var client = await ManagerClientAsync("prof.badperm@demo.local");

        var resp = await client.PostAsJsonAsync("/profiles", new
        {
            name = "Com permissão inválida",
            description = (string?)null,
            permissionKeys = new[] { "does.not.exist" },
        });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Update_changes_permissions_and_emits_audit()
    {
        var client = await ManagerClientAsync("prof.update@demo.local");
        var created = await (await client.PostAsJsonAsync("/profiles", new
        {
            name = "A editar",
            description = "antes",
            permissionKeys = new[] { AccessControlPermissions.UsersManage },
        })).Content.ReadFromJsonAsync<ProfileDetailDto>();

        // Troca a permissão (remove UsersManage, adiciona ProfilesAssign) e a descrição.
        var upd = await client.PutAsJsonAsync($"/profiles/{created!.Id}", new
        {
            name = "A editar",
            description = "depois",
            permissionKeys = new[] { AccessControlPermissions.ProfilesAssign },
        });
        Assert.Equal(HttpStatusCode.OK, upd.StatusCode);
        var updated = await upd.Content.ReadFromJsonAsync<ProfileDetailDto>();
        Assert.Contains(AccessControlPermissions.ProfilesAssign, updated!.PermissionKeys);
        Assert.DoesNotContain(AccessControlPermissions.UsersManage, updated.PermissionKeys);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
        var pid = created.Id.ToString();
        var granted = await db.AuditEvents.AnyAsync(a =>
            a.Action == "access_control.profile.permission_granted" && a.EntityId == pid);
        var revoked = await db.AuditEvents.AnyAsync(a =>
            a.Action == "access_control.profile.permission_revoked" && a.EntityId == pid);
        Assert.True(granted);
        Assert.True(revoked);
        // O evento tem ator preenchido (o usuário logado).
        var hasActor = await db.AuditEvents.AnyAsync(a => a.EntityId == pid && a.ActorUserId != null);
        Assert.True(hasActor);
    }

    [Fact]
    public async Task Archive_sets_status_and_blocks_system_profile()
    {
        var client = await ManagerClientAsync("prof.archive@demo.local");
        var created = await (await client.PostAsJsonAsync("/profiles", new
        {
            name = "Para arquivar", description = (string?)null, permissionKeys = new string[0],
        })).Content.ReadFromJsonAsync<ProfileDetailDto>();

        var archive = await client.DeleteAsync($"/profiles/{created!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, archive.StatusCode);
        var after = await client.GetFromJsonAsync<ProfileDetailDto>($"/profiles/{created.Id}");
        Assert.Equal(ProfileStatus.Archived.ToString(), after!.Status);

        // O perfil de sistema "Administrador" não pode ser arquivado nem editado.
        var orgId = await factory.GetDemoOrganizationIdAsync();
        Guid systemId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
            systemId = await db.Profiles.Where(p => p.OrganizationId == orgId && p.IsSystem).Select(p => p.Id).FirstAsync();
        }
        var blockedArchive = await client.DeleteAsync($"/profiles/{systemId}");
        Assert.Equal(HttpStatusCode.BadRequest, blockedArchive.StatusCode);
        var blockedEdit = await client.PutAsJsonAsync($"/profiles/{systemId}", new
        {
            name = "Hackeado", description = (string?)null, permissionKeys = new string[0],
        });
        Assert.Equal(HttpStatusCode.BadRequest, blockedEdit.StatusCode);
    }

    [Fact]
    public async Task Requires_profiles_manage()
    {
        var orgId = await factory.GetDemoOrganizationIdAsync();
        await factory.CreateUserAsync("prof.noperm@demo.local", "Passw0rd!", orgId, OrganizationRole.Member);
        var client = await factory.CreateLoggedInClientAsync("prof.noperm@demo.local", "Passw0rd!");

        var resp = await client.GetAsync("/profiles");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }
}
