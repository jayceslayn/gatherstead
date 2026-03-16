using Gatherstead.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Gatherstead.Api.Tests.Fixtures;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public JwtTokenHelper TokenHelper { get; } = new();

    private SqliteConnection? _connection;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // External identity provider config — values feed Program.cs; actual token
                // validation is overridden in ConfigureTestServices below.
                ["ExternalIdentity:Instance"] = "https://test-idp.example.com",
                ["ExternalIdentity:Domain"] = "test-idp.example.com",
                ["ExternalIdentity:ClientId"] = JwtTokenHelper.TestAudience,
                ["ExternalIdentity:SignUpSignInPolicyId"] = "B2C_1_test",
                ["ExternalIdentity:ValidIssuer"] = JwtTokenHelper.TestIssuer,
                ["ConnectionStrings:Default"] = "DataSource=:memory:",
                ["Cors:AllowedOrigins:0"] = "https://allowed-origin.example.com",
                ["RateLimiting:PermitLimit"] = "100",
                ["RateLimiting:WindowMinutes"] = "1",
                ["RateLimiting:QueueLimit"] = "0",
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

            // Rate limiting is configured by Program.cs using the config values above.
            // Individual test classes that need specific limits can subclass this factory.
        });

        // ConfigureTestServices runs AFTER the app's ConfigureServices (Program.cs),
        // so these overrides take precedence over the app's JWT Bearer config.
        builder.ConfigureTestServices(services =>
        {
            // Override JWT Bearer to use test signing key and bypass OIDC discovery.
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.Configuration = new OpenIdConnectConfiguration
                {
                    Issuer = JwtTokenHelper.TestIssuer,
                };
                options.Configuration.SigningKeys.Add(TokenHelper.SecurityKey);

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = JwtTokenHelper.TestIssuer,
                    ValidateAudience = true,
                    ValidAudience = JwtTokenHelper.TestAudience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = TokenHelper.SecurityKey,
                    ClockSkew = TimeSpan.FromMinutes(5),
                };
            });
        });
    }

    /// <summary>
    /// Seeds a user directly via SQL, bypassing the auditing interceptor.
    /// </summary>
    public void SeedUser(Guid userId, string externalId)
    {
        if (_connection == null)
        {
            // Ensure the host is built so the connection is initialized
            _ = Services;
        }

        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Users (Id, ExternalId, IsAppAdmin, CreatedAt, CreatedByUserId, UpdatedAt, UpdatedByUserId, IsDeleted)
            VALUES ($id, $externalId, 0, $now, $id, $now, $id, 0)
            """;
        cmd.Parameters.Add(new Microsoft.Data.Sqlite.SqliteParameter("$id", userId.ToString()));
        cmd.Parameters.Add(new Microsoft.Data.Sqlite.SqliteParameter("$externalId", externalId));
        cmd.Parameters.Add(new Microsoft.Data.Sqlite.SqliteParameter("$now", DateTimeOffset.UtcNow.ToString("O")));
        cmd.ExecuteNonQuery();
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
