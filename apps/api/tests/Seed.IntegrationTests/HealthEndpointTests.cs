using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Seed.IntegrationTests;

// Boots the whole API in-memory and checks the liveness endpoint.
// Proves the host, DI and routing are wired up (ADR-0003).
public class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_endpoint_returns_ok()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
