using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Seed.Application.AccessControl;
using Seed.Application.Companies;
using Seed.Domain.AccessControl;
using Seed.Infrastructure.Persistence;

namespace Seed.IntegrationTests;

// Concessão e revogação de acesso a empresa (companies.grant_access) e a regra
// de escopo concedível da ADR-0014.
public class CompanyAccessTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Grant_access_permission_is_reconciled_into_catalog()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();

        var permission = await db.Permissions
            .FirstOrDefaultAsync(p => p.Key == CompaniesPermissions.GrantAccess);
        Assert.NotNull(permission);
        Assert.Equal(PermissionStatus.Active, permission!.Status);
        Assert.Equal("organizations", permission.Module);

        // O perfil de sistema recebe toda permissão ativa no boot (top-up).
        var orgId = await factory.GetDemoOrganizationIdAsync();
        var systemProfileId = await db.Profiles
            .Where(p => p.OrganizationId == orgId && p.IsSystem).Select(p => p.Id).FirstAsync();
        Assert.True(await db.ProfilePermissions.AnyAsync(pp =>
            pp.ProfileId == systemProfileId && pp.PermissionKey == CompaniesPermissions.GrantAccess));
    }

    // Cria um usuário na org Demo e devolve seu id (sem perfil, sem empresa).
    private async Task<Guid> CreateMemberAsync(string email)
    {
        var orgId = await factory.GetDemoOrganizationIdAsync();
        await factory.CreateUserAsync(email, ApiFactory.MemberPassword, orgId);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
        return await db.Users.Where(u => u.Email == email).Select(u => u.Id).FirstAsync();
    }

    // Cria uma empresa na org Demo e, opcionalmente, concede acesso a alguém.
    private async Task<Guid> CreateCompanyAsync(string name, Guid? grantTo = null)
    {
        var orgId = await factory.GetDemoOrganizationIdAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
        var now = DateTime.UtcNow;
        var company = new Seed.Domain.Companies.Company
        {
            OrganizationId = orgId, Name = name, CreatedAt = now, UpdatedAt = now,
        };
        db.Companies.Add(company);
        if (grantTo is not null)
            db.UserCompanyAccesses.Add(new Seed.Domain.Access.UserCompanyAccess
            {
                UserId = grantTo.Value, CompanyId = company.Id, OrganizationId = orgId,
                CreatedAt = now, UpdatedAt = now,
            });
        await db.SaveChangesAsync();
        return company.Id;
    }

    private async Task<List<Guid>> CompaniesOfAsync(Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
        return await db.UserCompanyAccesses.Where(a => a.UserId == userId)
            .Select(a => a.CompanyId).ToListAsync();
    }

    // Concede um perfil ativo com as permissões dadas a um usuário JÁ existente.
    // Diferente de CreateClientWithPermissionsAsync, que cria o usuário do zero:
    // aqui o usuário precisa preexistir porque já recebeu empresas.
    private async Task GivePermissionsAsync(Guid userId, string profileName, params string[] permissionKeys)
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

    [Fact]
    public async Task Set_user_companies_requires_grant_access_permission()
    {
        // Tem users.manage, mas não companies.grant_access.
        var client = await factory.CreateClientWithPermissionsAsync(
            "acc.noperm@demo.local", AccessControlPermissions.UsersManage);
        var targetId = await CreateMemberAsync("acc.noperm.target@demo.local");
        var companyId = await CreateCompanyAsync("Emp NoPerm");

        var resp = await client.PutAsJsonAsync($"/users/{targetId}/companies",
            new { companyIds = new[] { companyId } });

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Owner_grants_and_revokes_any_company()
    {
        var owner = await factory.CreateAdminClientAsync();
        var targetId = await CreateMemberAsync("acc.target@demo.local");
        // Empresa órfã: ninguém tem acesso a ela.
        var orphanId = await CreateCompanyAsync("Emp Orfa");

        var grant = await owner.PutAsJsonAsync($"/users/{targetId}/companies",
            new { companyIds = new[] { orphanId } });
        Assert.Equal(HttpStatusCode.NoContent, grant.StatusCode);
        Assert.Contains(orphanId, await CompaniesOfAsync(targetId));

        var revoke = await owner.PutAsJsonAsync($"/users/{targetId}/companies",
            new { companyIds = Array.Empty<Guid>() });
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);
        Assert.Empty(await CompaniesOfAsync(targetId));
    }

    [Fact]
    public async Task Non_owner_cannot_grant_company_outside_own_scope()
    {
        var granterId = await CreateMemberAsync("acc.granter@demo.local");
        var mine = await CreateCompanyAsync("Emp Minha", grantTo: granterId);
        var outside = await CreateCompanyAsync("Emp Fora");
        await GivePermissionsAsync(granterId, "Perfil Granter", CompaniesPermissions.GrantAccess);
        var granter = await factory.CreateLoggedInClientAsync("acc.granter@demo.local", ApiFactory.MemberPassword);

        var targetId = await CreateMemberAsync("acc.scope.target@demo.local");

        // Dentro do escopo: concede.
        var ok = await granter.PutAsJsonAsync($"/users/{targetId}/companies",
            new { companyIds = new[] { mine } });
        Assert.Equal(HttpStatusCode.NoContent, ok.StatusCode);

        // Fora do escopo: 404 (não revela que a empresa existe).
        var denied = await granter.PutAsJsonAsync($"/users/{targetId}/companies",
            new { companyIds = new[] { mine, outside } });
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
    }

    [Fact]
    public async Task Grants_outside_caller_scope_are_preserved()
    {
        var granterId = await CreateMemberAsync("acc.preserve.granter@demo.local");
        var mine = await CreateCompanyAsync("Emp Preserva Minha", grantTo: granterId);
        var targetId = await CreateMemberAsync("acc.preserve.target@demo.local");
        // O alvo já tem a empresa que o granter não enxerga.
        var outside = await CreateCompanyAsync("Emp Preserva Fora", grantTo: targetId);
        await GivePermissionsAsync(granterId, "Perfil Preserva", CompaniesPermissions.GrantAccess);

        var granter = await factory.CreateLoggedInClientAsync(
            "acc.preserve.granter@demo.local", ApiFactory.MemberPassword);

        // Envia só o que enxerga. A concessão fora do escopo NÃO pode sumir.
        var resp = await granter.PutAsJsonAsync($"/users/{targetId}/companies",
            new { companyIds = new[] { mine } });
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);

        var companies = await CompaniesOfAsync(targetId);
        Assert.Contains(mine, companies);
        Assert.Contains(outside, companies);
    }

    [Fact]
    public async Task Cross_tenant_target_and_company_are_404()
    {
        var owner = await factory.CreateAdminClientAsync();
        var other = await factory.CreateSecondTenantAsync(
            orgName: "Org Acc", companyName: "Emp Acc",
            userEmail: "acc@other.local", userPassword: "Acc12345!");
        var localTarget = await CreateMemberAsync("acc.cross.target@demo.local");

        Guid foreignUserId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
            foreignUserId = await db.Users.Where(u => u.Email == "acc@other.local")
                .Select(u => u.Id).FirstAsync();
        }

        // Usuário de outra organização como alvo.
        var badTarget = await owner.PutAsJsonAsync($"/users/{foreignUserId}/companies",
            new { companyIds = Array.Empty<Guid>() });
        Assert.Equal(HttpStatusCode.NotFound, badTarget.StatusCode);

        // Empresa de outra organização no payload.
        var badCompany = await owner.PutAsJsonAsync($"/users/{localTarget}/companies",
            new { companyIds = new[] { other.CompanyId } });
        Assert.Equal(HttpStatusCode.NotFound, badCompany.StatusCode);
    }

    [Fact]
    public async Task Soft_deleted_company_is_404()
    {
        var owner = await factory.CreateAdminClientAsync();
        var targetId = await CreateMemberAsync("acc.deleted.target@demo.local");

        Guid ownerId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
            ownerId = await db.Users.Where(u => u.Email == ApiFactory.AdminEmail)
                .Select(u => u.Id).FirstAsync();
        }

        // Concedida ao próprio owner: sem acesso, o DELETE responderia 404 e o
        // teste passaria pelo motivo errado (empresa nunca excluída).
        var companyId = await CreateCompanyAsync("Emp Excluida", grantTo: ownerId);
        var deleted = await owner.DeleteAsync($"/companies/{companyId}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        // Exclusão é soft (DeletedAt). O filtro global de Company a tira do
        // escopo concedível, então ela some como se nunca tivesse existido.
        var resp = await owner.PutAsJsonAsync($"/users/{targetId}/companies",
            new { companyIds = new[] { companyId } });
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Owner_target_can_have_companies_changed()
    {
        var owner = await factory.CreateAdminClientAsync();
        var companyId = await CreateCompanyAsync("Emp Do Owner");

        Guid ownerId;
        List<Guid> current;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
            ownerId = await db.Users.Where(u => u.Email == ApiFactory.AdminEmail)
                .Select(u => u.Id).FirstAsync();
            // Só as empresas VIVAS, e não o CompaniesOfAsync cru: o acesso
            // sobrevive ao soft-delete da empresa, e reenviar uma empresa morta
            // daria 404 (ela saiu do escopo). A tela também não a listaria.
            current = await (
                from a in db.UserCompanyAccesses
                join c in db.Companies on a.CompanyId equals c.Id
                where a.UserId == ownerId
                select c.Id).ToListAsync();
        }

        var desired = current.Append(companyId).ToArray();

        // Ao contrário de status e perfis, o eixo de empresa é editável no owner.
        var resp = await owner.PutAsJsonAsync($"/users/{ownerId}/companies",
            new { companyIds = desired });
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
        Assert.Contains(companyId, await CompaniesOfAsync(ownerId));
    }

    [Fact]
    public async Task Grant_and_revoke_emit_audit_events()
    {
        var owner = await factory.CreateAdminClientAsync();
        var targetId = await CreateMemberAsync("acc.audit.target@demo.local");
        var companyId = await CreateCompanyAsync("Emp Auditada");

        await owner.PutAsJsonAsync($"/users/{targetId}/companies",
            new { companyIds = new[] { companyId } });
        await owner.PutAsJsonAsync($"/users/{targetId}/companies",
            new { companyIds = Array.Empty<Guid>() });

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
        var tid = targetId.ToString();

        var granted = await db.AuditEvents.FirstOrDefaultAsync(a =>
            a.Action == "organizations.user.company_access_granted" && a.EntityId == tid);
        var revoked = await db.AuditEvents.FirstOrDefaultAsync(a =>
            a.Action == "organizations.user.company_access_revoked" && a.EntityId == tid);

        Assert.NotNull(granted);
        Assert.NotNull(revoked);
        Assert.Equal("User", granted!.EntityType);
        Assert.Equal("User", revoked!.EntityType);

        // Contrato de vínculo da ADR-0013: chave técnica, rótulo humano e o par
        // old/new nos dois sentidos. Lê o JSON em vez de casar substring — o
        // formato do serializador não é contrato, o conteúdo é.
        using var grantedMeta = JsonDocument.Parse(granted.Metadata!);
        Assert.Equal(companyId.ToString(), grantedMeta.RootElement.GetProperty("company_id").GetString());
        Assert.Equal("Emp Auditada", grantedMeta.RootElement.GetProperty("company_name").GetString());
        Assert.False(grantedMeta.RootElement.GetProperty("old").GetBoolean());
        Assert.True(grantedMeta.RootElement.GetProperty("new").GetBoolean());

        using var revokedMeta = JsonDocument.Parse(revoked.Metadata!);
        Assert.Equal(companyId.ToString(), revokedMeta.RootElement.GetProperty("company_id").GetString());
        Assert.Equal("Emp Auditada", revokedMeta.RootElement.GetProperty("company_name").GetString());
        Assert.True(revokedMeta.RootElement.GetProperty("old").GetBoolean());
        Assert.False(revokedMeta.RootElement.GetProperty("new").GetBoolean());
    }

    [Fact]
    public async Task Company_axis_still_requires_companies_access_permission()
    {
        // Os dois eixos continuam independentes: ter UserCompanyAccess sem a
        // permissão funcional companies.access não dá acesso à listagem.
        var owner = await factory.CreateAdminClientAsync();
        var targetId = await CreateMemberAsync("acc.twoaxis@demo.local");
        var companyId = await CreateCompanyAsync("Emp Dois Eixos");

        await owner.PutAsJsonAsync($"/users/{targetId}/companies",
            new { companyIds = new[] { companyId } });

        var target = await factory.CreateLoggedInClientAsync("acc.twoaxis@demo.local", ApiFactory.MemberPassword);
        var resp = await target.GetAsync("/companies");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }
}
