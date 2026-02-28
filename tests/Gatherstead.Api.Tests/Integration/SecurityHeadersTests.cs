using Gatherstead.Api.Tests.Fixtures;

namespace Gatherstead.Api.Tests.Integration;

public class SecurityHeadersTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public SecurityHeadersTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Response_ContainsXContentTypeOptions()
    {
        var response = await _client.GetAsync("/", TestContext.Current.CancellationToken);

        Assert.True(response.Headers.Contains("X-Content-Type-Options"));
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").First());
    }

    [Fact]
    public async Task Response_ContainsXFrameOptions()
    {
        var response = await _client.GetAsync("/", TestContext.Current.CancellationToken);

        Assert.True(response.Headers.Contains("X-Frame-Options"));
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").First());
    }

    [Fact]
    public async Task Response_ContainsReferrerPolicy()
    {
        var response = await _client.GetAsync("/", TestContext.Current.CancellationToken);

        Assert.True(response.Headers.Contains("Referrer-Policy"));
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").First());
    }

    [Fact]
    public async Task Response_ContainsPermissionsPolicy()
    {
        var response = await _client.GetAsync("/", TestContext.Current.CancellationToken);

        Assert.True(response.Headers.Contains("Permissions-Policy"));
        Assert.Equal("geolocation=(), camera=(), microphone=()",
            response.Headers.GetValues("Permissions-Policy").First());
    }

    [Fact]
    public async Task AllSecurityHeaders_PresentOnEveryResponse()
    {
        var response = await _client.GetAsync("/", TestContext.Current.CancellationToken);

        Assert.True(response.Headers.Contains("X-Content-Type-Options"));
        Assert.True(response.Headers.Contains("X-Frame-Options"));
        Assert.True(response.Headers.Contains("Referrer-Policy"));
        Assert.True(response.Headers.Contains("Permissions-Policy"));

        // CSP may be in Content headers
        var allHeaders = response.Headers
            .Concat(response.Content.Headers)
            .ToDictionary(h => h.Key, h => h.Value);
        Assert.True(allHeaders.ContainsKey("Content-Security-Policy"));
    }
}
