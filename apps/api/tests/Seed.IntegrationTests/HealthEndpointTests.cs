using System.Net;

namespace Seed.IntegrationTests;

// Boots the whole API in-memory (com Postgres real via ApiFactory) e checa o
// endpoint de liveness. Prova que host, DI, routing e banco estão conectados.
public class HealthEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Health_endpoint_returns_ok()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
