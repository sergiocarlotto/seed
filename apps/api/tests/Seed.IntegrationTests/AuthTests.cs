using System.Net;
using System.Net.Http.Json;

namespace Seed.IntegrationTests;

// Autenticação no modelo multiempresa: login do admin semeado (Demo), /auth/me
// sem sessão (401) e /auth/me autenticado retornando organização + empresas.
public class AuthTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Login_seeded_admin_ok()
    {
        var client = factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/auth/login", new
        {
            email = ApiFactory.AdminEmail,
            password = ApiFactory.AdminPassword,
        });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Me_without_login_401()
    {
        var client = factory.CreateClient();
        var me = await client.GetAsync("/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Fact]
    public async Task Me_returns_org_and_companies()
    {
        var client = await factory.CreateAdminClientAsync();

        var me = await client.GetAsync("/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);

        var body = await me.Content.ReadAsStringAsync();
        Assert.Contains(ApiFactory.DemoCompanyName, body); // "Empresa Demo"
        Assert.Contains("organizationId", body);
        Assert.Contains("isOwner", body); // dono da org (substitui orgRole)
        Assert.Contains("permissions", body);
    }
}
