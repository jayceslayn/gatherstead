using System.Threading.RateLimiting;
using Gatherstead.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Gatherstead.Api.Tests.Fixtures;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public PasetoTokenHelper TokenHelper { get; } = new();

    private SqliteConnection? _connection;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:PublicKey"] = TokenHelper.PublicKeyBase64,
                ["Authentication:Audience"] = PasetoTokenHelper.TestAudience,
                ["Authentication:Issuer"] = PasetoTokenHelper.TestIssuer,
                ["Authentication:ValidateAudience"] = "true",
                ["Authentication:ValidateIssuer"] = "true",
                ["ConnectionStrings:Default"] = "DataSource=:memory:",
                ["Cors:AllowedOrigins:0"] = "https://allowed-origin.example.com",
                ["RateLimiting:PermitLimit"] = "5",
                ["RateLimiting:WindowMinutes"] = "1",
                ["RateLimiting:QueueLimit"] = "0",
                ["KeyVault:VaultUrl"] = ""
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove all DbContext-related registrations to avoid dual-provider conflict
            var dbContextDescriptors = services
                .Where(d => d.ServiceType.FullName?.Contains("EntityFrameworkCore") == true
                          || d.ServiceType == typeof(DbContextOptions<GathersteadDbContext>)
                          || d.ServiceType == typeof(DbContextOptions)
                          || d.ImplementationType?.FullName?.Contains("SqlServer") == true)
                .ToList();
            foreach (var descriptor in dbContextDescriptors)
                services.Remove(descriptor);

            // Create a persistent SQLite in-memory connection
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            services.AddDbContext<GathersteadDbContext>((sp, options) =>
            {
                options.UseSqlite(_connection);
            });

            // Build a temporary provider to initialize the database schema
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<GathersteadDbContext>();
            db.Database.EnsureCreated();

            // Re-register CORS with test-allowed origins.
            // The app's Program.cs reads config eagerly, so ConfigureAppConfiguration
            // overrides arrive too late — we must replace the service directly.
            services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy
                        .WithOrigins("https://allowed-origin.example.com")
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                });
            });

            // Re-register rate limiter with low limits for testing.
            // Same eager-read issue as CORS above.
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

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection?.Dispose();
        }
    }
}
