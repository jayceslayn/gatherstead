using Gatherstead.Data;
using Gatherstead.Data.Entities;
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
    /// Set <paramref name="isAppAdmin"/> to grant App Admin, which bypasses tenant membership and
    /// role checks (used to reach controller actions without seeding a full tenant).
    /// </summary>
    public void SeedUser(Guid userId, string externalId, bool isAppAdmin = false)
    {
        if (_connection == null)
        {
            // Ensure the host is built so the connection is initialized
            _ = Services;
        }

        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Users (Id, ExternalId, IsAppAdmin, CreatedAt, CreatedByUserId, UpdatedAt, UpdatedByUserId, IsDeleted)
            VALUES ($id, $externalId, $isAppAdmin, $now, $id, $now, $id, 0)
            """;
        // Bind the Guid/bool as typed values (not hand-formatted strings) so Microsoft.Data.Sqlite
        // serializes them exactly as EF Core reads them back — otherwise a `Where(u => u.Id == guid)`
        // equality comparison won't match the stored value.
        cmd.Parameters.Add(new Microsoft.Data.Sqlite.SqliteParameter("$id", userId));
        cmd.Parameters.Add(new Microsoft.Data.Sqlite.SqliteParameter("$externalId", externalId));
        cmd.Parameters.Add(new Microsoft.Data.Sqlite.SqliteParameter("$isAppAdmin", isAppAdmin));
        cmd.Parameters.Add(new Microsoft.Data.Sqlite.SqliteParameter("$now", DateTimeOffset.UtcNow.ToString("O")));
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Seeds a tenant directly via SQL, bypassing the auditing interceptor. Pair with
    /// <see cref="SeedTenantUser"/> to give a user a real membership role, which is what lets a request
    /// clear <c>RequireTenantAccessAttribute</c> and reach the service layer's own authorization.
    /// </summary>
    public void SeedTenant(Guid tenantId, string name, Guid actorUserId)
    {
        if (_connection == null)
            _ = Services;

        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Tenants (Id, Name, CreatedAt, CreatedByUserId, UpdatedAt, UpdatedByUserId, IsDeleted)
            VALUES ($id, $name, $now, $actor, $now, $actor, 0)
            """;
        cmd.Parameters.Add(new SqliteParameter("$id", tenantId));
        cmd.Parameters.Add(new SqliteParameter("$name", name));
        cmd.Parameters.Add(new SqliteParameter("$actor", actorUserId));
        cmd.Parameters.Add(new SqliteParameter("$now", DateTimeOffset.UtcNow.ToString("O")));
        cmd.ExecuteNonQuery();
    }

    /// <summary>Seeds a <c>TenantUser</c> membership row (composite PK, no Id column) via SQL.</summary>
    public void SeedTenantUser(Guid tenantId, Guid userId, TenantRole role)
    {
        if (_connection == null)
            _ = Services;

        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = """
            INSERT INTO TenantUsers (TenantId, UserId, Role, CreatedAt, CreatedByUserId, UpdatedAt, UpdatedByUserId, IsDeleted)
            VALUES ($tenantId, $userId, $role, $now, $userId, $now, $userId, 0)
            """;
        cmd.Parameters.Add(new SqliteParameter("$tenantId", tenantId));
        cmd.Parameters.Add(new SqliteParameter("$userId", userId));
        cmd.Parameters.Add(new SqliteParameter("$role", (int)role));
        cmd.Parameters.Add(new SqliteParameter("$now", DateTimeOffset.UtcNow.ToString("O")));
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
