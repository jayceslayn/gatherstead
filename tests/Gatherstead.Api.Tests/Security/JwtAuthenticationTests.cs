using System.Net;
using System.Net.Http.Headers;
using Gatherstead.Api.Tests.Fixtures;

namespace Gatherstead.Api.Tests.Security;

public class JwtAuthenticationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public JwtAuthenticationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task NoAuthorizationHeader_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/tenants", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task EmptyBearerToken_Returns401()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "");

        var response = await client.GetAsync("/api/tenants", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task NonBearerScheme_Returns401()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", "dGVzdDp0ZXN0");

        var response = await client.GetAsync("/api/tenants", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MalformedToken_Returns401()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-valid-jwt");

        var response = await client.GetAsync("/api/tenants", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ValidToken_ReturnsSuccess()
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
    public async Task ExpiredToken_Returns401()
    {
        var client = _factory.CreateClient();
        var token = _factory.TokenHelper.GenerateToken(
            sub: Guid.NewGuid().ToString(),
            iat: DateTime.UtcNow.AddHours(-2),
            exp: DateTime.UtcNow.AddHours(-1));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/tenants", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task InvalidSignature_Returns401()
    {
        var client = _factory.CreateClient();
        var token = _factory.TokenHelper.GenerateTokenWithDifferentKey();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/tenants", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WrongAudience_Returns401()
    {
        var client = _factory.CreateClient();
        var token = _factory.TokenHelper.GenerateToken(
            sub: Guid.NewGuid().ToString(),
            audience: "wrong-audience");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/tenants", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WrongIssuer_Returns401()
    {
        var client = _factory.CreateClient();
        var token = _factory.TokenHelper.GenerateToken(
            sub: Guid.NewGuid().ToString(),
            issuer: "https://wrong-issuer.example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/tenants", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
