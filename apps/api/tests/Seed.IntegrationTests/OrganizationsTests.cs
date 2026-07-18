using System.Net;
using System.Net.Http.Json;

namespace Seed.IntegrationTests;

public class OrganizationsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private async Task<HttpClient> RegisterClient(string email, string org)
    {
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/auth/register", new
        {
            organizationName = org, fullName = "User", email, password = "Senha123!"
        });
        return client;
    }

    [Fact]
    public async Task Owner_can_create_and_list()
    {
        var client = await RegisterClient("owner1@x.com", "Org1");
        var create = await client.PostAsJsonAsync("/organizations", new { name = "Nova" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var list = await client.GetFromJsonAsync<List<Dictionary<string, object>>>("/organizations");
        Assert.True(list!.Count >= 2); // Org1 (registro) + Nova
    }

    [Fact]
    public async Task Cross_tenant_get_returns_404()
    {
        var a = await RegisterClient("a@x.com", "OrgA");
        var created = await a.PostAsJsonAsync("/organizations", new { name = "SoDoA" });
        var id = (await created.Content.ReadFromJsonAsync<Dictionary<string, object>>())!["id"].ToString();

        var b = await RegisterClient("b@x.com", "OrgB");
        var resp = await b.GetAsync($"/organizations/{id}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }
}
