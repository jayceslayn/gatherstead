using Gatherstead.Api.Tests.Fixtures;

namespace Gatherstead.Api.Tests.Integration;

public class CorsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public CorsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PreflightRequest_AllowedOrigin_ReturnsAccessControlHeaders()
    {
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/tenants");
        request.Headers.Add("Origin", "https://allowed-origin.example.com");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.True(response.Headers.Contains("Access-Control-Allow-Origin"),
            "Expected Access-Control-Allow-Origin header on preflight response");
        Assert.Equal("https://allowed-origin.example.com",
            response.Headers.GetValues("Access-Control-Allow-Origin").First());
    }

    [Fact]
    public async Task PreflightRequest_DisallowedOrigin_NoAccessControlHeaders()
    {
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/tenants");
        request.Headers.Add("Origin", "https://evil-origin.example.com");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task SimpleRequest_AllowedOrigin_ReturnsAccessControlAllowOrigin()
    {
        var client = _factory.CreateClient();

        // Use a valid token so the request reaches the endpoint (not blocked by auth)
        var token = _factory.TokenHelper.GenerateToken(sub: Guid.NewGuid().ToString());
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/tenants");
        request.Headers.Add("Origin", "https://allowed-origin.example.com");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.True(response.Headers.Contains("Access-Control-Allow-Origin"),
            "Expected Access-Control-Allow-Origin header on simple cross-origin request");
        Assert.Equal("https://allowed-origin.example.com",
            response.Headers.GetValues("Access-Control-Allow-Origin").First());
    }
}
