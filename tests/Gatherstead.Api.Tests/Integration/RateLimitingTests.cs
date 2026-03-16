using System.Net;
using System.Threading.RateLimiting;
using Gatherstead.Api.Tests.Fixtures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Gatherstead.Api.Tests.Integration;

public class RateLimitingTests : IAsyncLifetime
{
    private RateLimitingFactory _factory = null!;

    /// <summary>
    /// Factory with a low rate limit (5 requests/minute) for testing rate limiting behavior.
    /// </summary>
    private class RateLimitingFactory : CustomWebApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.ConfigureTestServices(services =>
            {
                services.AddRateLimiter(options =>
                {
                    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                    {
                        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                        return RateLimitPartition.GetFixedWindowLimiter(
                            partitionKey: ipAddress,
                            factory: _ => new FixedWindowRateLimiterOptions
                            {
                                PermitLimit = 5,
                                Window = TimeSpan.FromMinutes(1),
                                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                                QueueLimit = 0
                            });
                    });

                    options.OnRejected = async (context, cancellationToken) =>
                    {
                        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                        await context.HttpContext.Response.WriteAsJsonAsync(
                            new { error = "Too many requests. Please try again later." },
                            cancellationToken: cancellationToken);
                    };
                });
            });
        }
    }

    public ValueTask InitializeAsync()
    {
        // Each test class gets its own factory so the rate limiter isn't
        // polluted by requests from other integration test classes.
        _factory = new RateLimitingFactory();
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
        // Factory configures PermitLimit=5; hit a path that doesn't require auth
        var client = _factory.CreateClient();

        HttpStatusCode lastStatus = HttpStatusCode.OK;
        for (var i = 0; i < 20; i++)
        {
            var response = await client.GetAsync("/", TestContext.Current.CancellationToken);
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
            var response = await client.GetAsync("/", TestContext.Current.CancellationToken);
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
