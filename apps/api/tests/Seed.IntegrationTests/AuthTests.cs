using System.Net;
using System.Net.Http.Json;

namespace Seed.IntegrationTests;

public class AuthTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Register_then_me_returns_user_and_org()
    {
        var client = factory.CreateClient();
        var reg = await client.PostAsJsonAsync("/auth/register", new
        {
            organizationName = "Acme", fullName = "Ana", email = "ana@acme.com", password = "Senha123!"
        });
        Assert.Equal(HttpStatusCode.OK, reg.StatusCode);

        var me = await client.GetAsync("/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var body = await me.Content.ReadAsStringAsync();
        Assert.Contains("Acme", body);
    }

    [Fact]
    public async Task Me_without_login_is_401()
    {
        var client = factory.CreateClient();
        var me = await client.GetAsync("/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }
}
