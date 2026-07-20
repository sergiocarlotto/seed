using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Seed.Application.Companies;
using Seed.Domain.Access;
using Seed.Domain.AccessControl;
using Seed.Domain.Organizations;
using Seed.Infrastructure.Persistence;

namespace Seed.IntegrationTests;

// Enforcement do gate de empresas por permissão (companies.access / companies.manage),
// que substitui o antigo orgRole. Também prova que um usuário desativado é bloqueado
// em /companies — fechamento do risco residual do Plano 3c.
public class CompaniesEnforcementTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private async Task<Guid> CreateMemberAsync(string email)
    {
        var orgId = await factory.GetDemoOrganizationIdAsync();
        await factory.CreateUserAsync(email, "Passw0rd!", orgId, OrganizationRole.Member);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
        return await db.Users.Where(u => u.Email == email).Select(u => u.Id).FirstAsync();
    }

    private async Task GiveProfileAsync(Guid userId, string profileName, params string[] keys)
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
        foreach (var k in keys)
            db.ProfilePermissions.Add(new ProfilePermission { ProfileId = profile.Id, PermissionKey = k });
        db.UserProfiles.Add(new UserProfile { UserId = userId, ProfileId = profile.Id });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task No_companies_permission_is_forbidden()
    {
        // Membro sem perfil: nenhuma permissão funcional → nem lista empresas.
        await CreateMemberAsync("comp.noperm@demo.local");
        var client = await factory.CreateLoggedInClientAsync("comp.noperm@demo.local", "Passw0rd!");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/companies")).StatusCode);
    }

    [Fact]
    public async Task Access_lists_but_manage_is_required_to_create()
    {
        var userId = await CreateMemberAsync("comp.access@demo.local");
        await GiveProfileAsync(userId, "Só Acesso Empresas", CompaniesPermissions.Access);
        var client = await factory.CreateLoggedInClientAsync("comp.access@demo.local", "Passw0rd!");

        // companies.access basta para listar (mesmo sem UserCompanyAccess → lista vazia, 200).
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/companies")).StatusCode);
        // Mas criar exige companies.manage.
        var create = await client.PostAsJsonAsync("/companies", new { name = "Não pode" });
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
    }

    [Fact]
    public async Task Manage_permission_can_create()
    {
        var userId = await CreateMemberAsync("comp.manage@demo.local");
        await GiveProfileAsync(userId, "Gestor de Empresas",
            CompaniesPermissions.Access, CompaniesPermissions.Manage);
        var client = await factory.CreateLoggedInClientAsync("comp.manage@demo.local", "Passw0rd!");

        var create = await client.PostAsJsonAsync("/companies", new { name = "Filial via permissão" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
    }

    [Fact]
    public async Task Deactivated_user_is_blocked_on_companies()
    {
        // Fechamento do risco residual do 3c: com o gate por permissão, desativar
        // bloqueia /companies imediatamente (antes, /companies era só [Authorize]).
        var userId = await CreateMemberAsync("comp.deact@demo.local");
        await GiveProfileAsync(userId, "Acesso Empresas Deact", CompaniesPermissions.Access);
        var client = await factory.CreateLoggedInClientAsync("comp.deact@demo.local", "Passw0rd!");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/companies")).StatusCode);

        var owner = await factory.CreateAdminClientAsync();
        var deact = await owner.PatchAsJsonAsync($"/users/{userId}/status", new { active = false });
        Assert.Equal(HttpStatusCode.OK, deact.StatusCode);

        // Sessão ainda válida, mas permissão efetiva agora é vazia → 403.
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/companies")).StatusCode);
    }

    [Fact]
    public async Task Auth_me_gates_companies_by_permission_and_status()
    {
        // Membro com companies.access e acesso à empresa Demo: /auth/me lista a empresa.
        var userId = await CreateMemberAsync("comp.me@demo.local");
        await GiveProfileAsync(userId, "Acesso Empresas Me", CompaniesPermissions.Access);

        var orgId = await factory.GetDemoOrganizationIdAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
            var companyId = await db.Companies
                .Where(c => c.OrganizationId == orgId && c.Name == ApiFactory.DemoCompanyName)
                .Select(c => c.Id).FirstAsync();
            var now = DateTime.UtcNow;
            db.UserCompanyAccesses.Add(new UserCompanyAccess
            {
                UserId = userId, CompanyId = companyId, OrganizationId = orgId,
                CreatedAt = now, UpdatedAt = now,
            });
            await db.SaveChangesAsync();
        }

        var client = await factory.CreateLoggedInClientAsync("comp.me@demo.local", "Passw0rd!");
        var me1 = await client.GetFromJsonAsync<MeCompanies>("/auth/me");
        Assert.Contains(me1!.Companies, c => c.Name == ApiFactory.DemoCompanyName);

        // Desativado: /auth/me deixa de listar empresas (permissão efetiva vazia → gate).
        var owner = await factory.CreateAdminClientAsync();
        await owner.PatchAsJsonAsync($"/users/{userId}/status", new { active = false });
        var me2 = await client.GetFromJsonAsync<MeCompanies>("/auth/me");
        Assert.Empty(me2!.Companies);
    }

    // Espelho enxuto do payload de /auth/me para desserializar só as empresas.
    private record MeCompanies(List<CompanyRef> Companies);
    private record CompanyRef(Guid Id, string Name);
}
