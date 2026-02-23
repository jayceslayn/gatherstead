using Gatherstead.Api.Services;
using Gatherstead.Api.Services.Households;
using Gatherstead.Api.Services.Tenants;
using Gatherstead.Api.Security;
using Gatherstead.Data;
using Gatherstead.Data.Interceptors;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserContext, HttpContextCurrentUserContext>();
builder.Services.AddScoped<ICurrentTenantContext, HttpContextCurrentTenantContext>();
builder.Services.AddScoped<AuditingSaveChangesInterceptor>();
builder.Services.AddScoped<IHouseholdService, HouseholdService>();
builder.Services.AddScoped<ITenantService, TenantService>();
builder.Services.AddScoped<ITokenRevocationService, TokenRevocationService>();

// Configure Azure Key Vault (if available)
Azure.Security.KeyVault.Secrets.SecretClient? secretClient = null;
var keyVaultUrl = builder.Configuration["KeyVault:VaultUrl"];
if (!string.IsNullOrEmpty(keyVaultUrl))
{
    secretClient = new Azure.Security.KeyVault.Secrets.SecretClient(
        new Uri(keyVaultUrl),
        new Azure.Identity.DefaultAzureCredential()
    );
}

builder.Services
    .AddAuthentication(PasetoAuthenticationDefaults.AuthenticationScheme)
    .AddScheme<PasetoAuthenticationOptions, PasetoAuthenticationHandler>(
        PasetoAuthenticationDefaults.AuthenticationScheme,
        options =>
        {
            var authenticationSection = builder.Configuration.GetSection("Authentication");

            // Load public key from Key Vault or fallback to configuration
            string? publicKey = null;
            var secretName = authenticationSection["PublicKeySecretName"];

            if (secretClient != null && !string.IsNullOrEmpty(secretName))
            {
                try
                {
                    var publicKeySecret = secretClient.GetSecret(secretName);
                    publicKey = publicKeySecret.Value.Value;
                }
                catch (Exception ex)
                {
                    // Log warning and fall back to configuration
                    using var loggerFactory = LoggerFactory.Create(logging => logging.AddConsole());
                    var logger = loggerFactory.CreateLogger<Program>();
                    logger.LogWarning(ex, "Failed to load public key from Key Vault, falling back to configuration");
                }
            }

            // Fallback to configuration if Key Vault not available or failed
            if (string.IsNullOrEmpty(publicKey))
            {
                publicKey = authenticationSection["PublicKey"];
            }

            if (string.IsNullOrEmpty(publicKey))
            {
                throw new InvalidOperationException(
                    "Authentication public key is required. Configure either KeyVault:VaultUrl with " +
                    "Authentication:PublicKeySecretName, or Authentication:PublicKey in appsettings.");
            }

            options.PublicKeyBase64 = publicKey;
            options.Audience = authenticationSection["Audience"];
            options.Issuer = authenticationSection["Issuer"];
            options.ImplicitAssertion = authenticationSection["ImplicitAssertion"];

            // Apply validation options from config
            if (TimeSpan.TryParse(authenticationSection["ClockSkew"], out var clockSkew))
                options.ClockSkew = clockSkew;
            if (TimeSpan.TryParse(authenticationSection["MaxTokenAge"], out var maxTokenAge))
                options.MaxTokenAge = maxTokenAge;
            if (bool.TryParse(authenticationSection["RequireExpirationTime"], out var requireExpirationTime))
                options.RequireExpirationTime = requireExpirationTime;
            if (bool.TryParse(authenticationSection["ValidateAudience"], out var validateAudience))
                options.ValidateAudience = validateAudience;
            if (bool.TryParse(authenticationSection["ValidateIssuer"], out var validateIssuer))
                options.ValidateIssuer = validateIssuer;
        });

builder.Services.AddAuthorization();

// Add rate limiting services
builder.Services.AddRateLimiter(options =>
{
    // Global rate limiter based on IP address
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ipAddress,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        var logger = context.HttpContext.RequestServices
            .GetRequiredService<ILogger<Program>>();
        logger.LogWarning(
            "Rate limit exceeded for IP: {IpAddress}, Endpoint: {Endpoint}",
            context.HttpContext.Connection.RemoteIpAddress,
            context.HttpContext.Request.Path
        );

        await context.HttpContext.Response.WriteAsJsonAsync(
            new { error = "Too many requests. Please try again later." },
            cancellationToken: cancellationToken
        );
    };
});

builder.Services.AddDbContext<GathersteadDbContext>((serviceProvider, options) =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var connectionString = configuration.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Connection string 'Default' is required.");

    options
        .UseSqlServer(connectionString);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
