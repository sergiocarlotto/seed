using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Seed.Application.AccessControl;
using Seed.Domain.AccessControl;
using Seed.Domain.Organizations;
using Seed.Infrastructure.Persistence;

namespace Seed.IntegrationTests;

// Criação de usuário (POST /users, gate users.manage). Cobre allow-list,
// estado inicial inócuo, mensagem neutra de e-mail duplicado e auditoria.
public class UserProvisioningTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    // Client logado como um gestor com as permissões dadas (perfil próprio).
    private async Task<HttpClient> ClientWithAsync(string email, params string[] permissionKeys)
    {
        var orgId = await factory.GetDemoOrganizationIdAsync();
        await factory.CreateUserAsync(email, "Passw0rd!", orgId);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
            var userId = await db.Users.Where(u => u.Email == email).Select(u => u.Id).FirstAsync();
            var now = DateTime.UtcNow;
            var profile = new Profile
            {
                OrganizationId = orgId, Name = $"Perfil {email}", Status = ProfileStatus.Active,
                CreatedAt = now, UpdatedAt = now,
            };
            db.Profiles.Add(profile);
            foreach (var key in permissionKeys)
                db.ProfilePermissions.Add(new ProfilePermission { ProfileId = profile.Id, PermissionKey = key });
            db.UserProfiles.Add(new UserProfile { UserId = userId, ProfileId = profile.Id });
            await db.SaveChangesAsync();
        }
        return await factory.CreateLoggedInClientAsync(email, "Passw0rd!");
    }

    [Fact]
    public async Task Create_requires_users_manage()
    {
        var orgId = await factory.GetDemoOrganizationIdAsync();
        await factory.CreateUserAsync("prov.noperm@demo.local", "Passw0rd!", orgId);
        var client = await factory.CreateLoggedInClientAsync("prov.noperm@demo.local", "Passw0rd!");

        var resp = await client.PostAsJsonAsync("/users", new
        {
            fullName = "Sem Permissão", email = "prov.blocked@demo.local", password = "Passw0rd!",
        });

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Create_returns_201_and_user_starts_with_no_power()
    {
        var manager = await ClientWithAsync("prov.mgr@demo.local", AccessControlPermissions.UsersManage);

        var resp = await manager.PostAsJsonAsync("/users", new
        {
            fullName = "Maria Silva", email = "prov.maria@demo.local", password = "Passw0rd!",
        });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var created = await resp.Content.ReadFromJsonAsync<UserDto>();
        Assert.NotNull(created);
        Assert.Equal("Maria Silva", created!.FullName);
        Assert.Equal(UserStatus.Active.ToString(), created.Status);
        Assert.False(created.IsOwner);
        Assert.Empty(created.Profiles);
        Assert.Empty(created.Companies);

        // A conta nasce inócua: loga, mas /auth/me não traz permissão nem empresa.
        var newbie = await factory.CreateLoggedInClientAsync("prov.maria@demo.local", "Passw0rd!");
        var me = await newbie.GetFromJsonAsync<MeResponse>("/auth/me");
        Assert.NotNull(me);
        Assert.False(me!.IsOwner);
        Assert.Empty(me.Permissions);
        Assert.Empty(me.Companies);
    }

    [Fact]
    public async Task Create_ignores_client_supplied_sensitive_fields()
    {
        var manager = await ClientWithAsync("prov.mass@demo.local", AccessControlPermissions.UsersManage);
        var otherOrg = await factory.CreateSecondTenantAsync(
            orgName: "Org Mass", companyName: "Emp Mass",
            userEmail: "mass@other.local", userPassword: "Mass123!");

        // Campos sensíveis no JSON não existem no DTO — devem ser simplesmente
        // ignorados, jamais aplicados (anti mass-assignment).
        var resp = await manager.PostAsJsonAsync("/users", new
        {
            fullName = "Tentativa Escalada", email = "prov.escalate@demo.local", password = "Passw0rd!",
            isOwner = true, status = "Inactive", organizationId = otherOrg.OrganizationId,
        });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var created = await resp.Content.ReadFromJsonAsync<UserDto>();
        Assert.False(created!.IsOwner);
        Assert.Equal(UserStatus.Active.ToString(), created.Status);

        var demoOrgId = await factory.GetDemoOrganizationIdAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
        var stored = await db.Users.FirstAsync(u => u.Email == "prov.escalate@demo.local");
        Assert.Equal(demoOrgId, stored.OrganizationId); // sempre a org do caller
    }

    [Fact]
    public async Task Create_with_duplicate_email_is_400_with_neutral_message()
    {
        var manager = await ClientWithAsync("prov.dup@demo.local", AccessControlPermissions.UsersManage);
        await factory.CreateSecondTenantAsync(
            orgName: "Org Dup", companyName: "Emp Dup",
            userEmail: "prov.taken@other.local", userPassword: "Dup1234!");

        // O e-mail existe em OUTRA organização: a resposta não pode revelar isso.
        var resp = await manager.PostAsJsonAsync("/users", new
        {
            fullName = "Colisão", email = "prov.taken@other.local", password = "Passw0rd!",
        });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Org Dup", body);
        Assert.DoesNotContain("já", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_with_weak_password_is_400()
    {
        var manager = await ClientWithAsync("prov.weak@demo.local", AccessControlPermissions.UsersManage);

        var resp = await manager.PostAsJsonAsync("/users", new
        {
            fullName = "Senha Fraca", email = "prov.weak.target@demo.local", password = "abc",
        });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Create_emits_audit_without_credentials()
    {
        var manager = await ClientWithAsync("prov.audit@demo.local", AccessControlPermissions.UsersManage);

        var resp = await manager.PostAsJsonAsync("/users", new
        {
            fullName = "Auditado", email = "prov.audited@demo.local", password = "Sup3rSenha!",
        });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var created = await resp.Content.ReadFromJsonAsync<UserDto>();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
        var cid = created!.Id.ToString();
        var ev = await db.AuditEvents.FirstOrDefaultAsync(
            a => a.Action == "access_control.user.created" && a.EntityId == cid);

        Assert.NotNull(ev);
        Assert.Equal("User", ev!.EntityType);
        Assert.NotNull(ev.ActorUserId);
        Assert.Contains("prov.audited@demo.local", ev.Metadata);
        // Nenhum resquício de credencial no metadata (ADR-0013, seção 3).
        Assert.DoesNotContain("Sup3rSenha!", ev.Metadata);
        Assert.DoesNotContain("password", ev.Metadata, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hash", ev.Metadata, StringComparison.OrdinalIgnoreCase);
    }

    // Espelho enxuto do payload de /auth/me para desserialização nos testes.
    private record MeResponse(bool IsOwner, List<string> Permissions, List<CompanyRef> Companies);
    private record CompanyRef(Guid Id, string Name);
}
