using System.Net;
using System.Net.Http.Json;
using Seed.Application.Companies;

namespace Seed.IntegrationTests;

// CRUD de empresa no modelo multiempresa: acesso explícito por usuário,
// isolamento entre organizações (cross-tenant) e restrição de papel (Member).
public class CompaniesTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Admin_lists_only_accessible()
    {
        var client = await factory.CreateAdminClientAsync();

        var companies = await client.GetFromJsonAsync<List<CompanyDto>>("/companies");

        Assert.NotNull(companies);
        Assert.Contains(companies!, c => c.Name == ApiFactory.DemoCompanyName);
    }

    [Fact]
    public async Task Admin_creates_company_and_sees_it()
    {
        var client = await factory.CreateAdminClientAsync();

        var create = await client.PostAsJsonAsync("/companies", new { name = "Filial Nova" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var companies = await client.GetFromJsonAsync<List<CompanyDto>>("/companies");
        Assert.NotNull(companies);
        Assert.Contains(companies!, c => c.Name == "Filial Nova");
    }

    [Fact]
    public async Task Member_cannot_create()
    {
        var orgId = await factory.GetDemoOrganizationIdAsync();
        await factory.CreateUserAsync("member@demo.local", "Member123!", orgId);

        var client = await factory.CreateLoggedInClientAsync("member@demo.local", "Member123!");

        var create = await client.PostAsJsonAsync("/companies", new { name = "Tentativa" });
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
    }

    [Fact]
    public async Task No_access_get_returns_404()
    {
        var other = await factory.CreateSecondTenantAsync(
            orgName: "Org GET 404",
            companyName: "Empresa GET 404",
            userEmail: "get404@outra.local");

        var admin = await factory.CreateAdminClientAsync();

        // Admin da Demo tenta ver empresa de OUTRA organização (sem acesso) → 404.
        var resp = await admin.GetAsync($"/companies/{other.CompanyId}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Cross_tenant_isolation()
    {
        var other = await factory.CreateSecondTenantAsync(
            orgName: "Org Cross",
            companyName: "Empresa Cross",
            userEmail: "cross@outra.local");

        var client = await factory.CreateLoggedInClientAsync(other.UserEmail, other.UserPassword);

        var companies = await client.GetFromJsonAsync<List<CompanyDto>>("/companies");
        Assert.NotNull(companies);
        // Vê a própria empresa, mas nunca a "Empresa Demo" da organização Demo.
        Assert.Contains(companies!, c => c.Name == other.CompanyName);
        Assert.DoesNotContain(companies!, c => c.Name == ApiFactory.DemoCompanyName);
    }
}
