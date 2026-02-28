using System.Net;
using Gatherstead.Api.Tests.Fixtures;

namespace Gatherstead.Api.Tests.Integration;

public class RateLimitingTests : IAsyncLifetime
{
    private CustomWebApplicationFactory _factory = null!;

    public ValueTask InitializeAsync()
    {
        // Each test class gets its own factory so the rate limiter isn't
        // polluted by requests from other integration test classes.
        _factory = new CustomWebApplicationFactory();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task WithinLimit_DoesNotReturn429()
    {
        // Factory configures PermitLimit=5
        var client = _factory.CreateClient();

        for (var i = 0; i < 5; i++)
        {
            var response = await client.GetAsync("/", TestContext.Current.CancellationToken);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }
    }

    [Fact]
    public async Task ExceedsLimit_Returns429()
    {
        // Factory configures PermitLimit=5
        var client = _factory.CreateClient();

        HttpStatusCode lastStatus = HttpStatusCode.OK;
        for (var i = 0; i < 20; i++)
        {
            var response = await client.GetAsync("/api/tenants", TestContext.Current.CancellationToken);
            lastStatus = response.StatusCode;
            if (lastStatus == HttpStatusCode.TooManyRequests)
                break;
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, lastStatus);
    }

    [Fact]
    public async Task RateLimitResponse_ContainsErrorMessage()
    {
        var client = _factory.CreateClient();

        HttpResponseMessage? rateLimitedResponse = null;
        for (var i = 0; i < 20; i++)
        {
            var response = await client.GetAsync("/api/tenants", TestContext.Current.CancellationToken);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                rateLimitedResponse = response;
                break;
            }
        }

        Assert.NotNull(rateLimitedResponse);
        var content = await rateLimitedResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Too many requests", content);
    }
}
