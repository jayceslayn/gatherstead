using System.Net;
using System.Net.Http.Headers;
using Gatherstead.Api.Tests.Fixtures;

namespace Gatherstead.Api.Tests.Integration;

public class AuthenticationPipelineTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AuthenticationPipelineTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task NoToken_ProtectedEndpoint_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/tenants", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ValidToken_ProtectedEndpoint_ReturnsSuccess()
    {
        var userId = Guid.NewGuid();
        var externalId = Guid.NewGuid().ToString();
        _factory.SeedUser(userId, externalId);

        var client = _factory.CreateClient();
        var token = _factory.TokenHelper.GenerateToken(sub: externalId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/tenants", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task InvalidToken_ProtectedEndpoint_Returns401()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-token");

        var response = await client.GetAsync("/api/tenants", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
