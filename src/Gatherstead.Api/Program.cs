using Gatherstead.Api.Encryption;
using Gatherstead.Api.Services.Households;
using Gatherstead.Api.Security;
using Gatherstead.Db;
using Gatherstead.Db.Encryption;
using Gatherstead.Db.Interceptors;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserContext, HttpContextCurrentUserContext>();
builder.Services.AddScoped<ICurrentTenantContext, HttpContextCurrentTenantContext>();
builder.Services.AddScoped<AuditingSaveChangesInterceptor>();
builder.Services.AddScoped<IHouseholdService, HouseholdService>();

builder.Services
    .AddAuthentication(PasetoAuthenticationDefaults.AuthenticationScheme)
    .AddScheme<PasetoAuthenticationOptions, PasetoAuthenticationHandler>(
        PasetoAuthenticationDefaults.AuthenticationScheme,
        options =>
        {
            var authenticationSection = builder.Configuration.GetSection("Authentication");
            options.PublicKeyBase64 = authenticationSection["PublicKey"]
                ?? throw new InvalidOperationException("Authentication:PublicKey configuration is required.");
            options.Audience = authenticationSection["Audience"];
            options.Issuer = authenticationSection["Issuer"];
            options.ImplicitAssertion = authenticationSection["ImplicitAssertion"];
        });

builder.Services.AddAuthorization();

builder.Services.AddDbContext<GathersteadDbContext>((serviceProvider, options) =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var connectionString = configuration.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Connection string 'Default' is required.");

    options
        .UseSqlServer(connectionString);
});

builder.Services.Configure<EncryptionOptions>(
    builder.Configuration.GetSection(EncryptionOptions.SectionName));
builder.Services.AddSingleton<IEncryptionKeyProvider, KeyVaultEncryptionKeyProvider>();
builder.Services.AddHealthChecks()
    .AddCheck<EncryptionHealthCheck>("encryption");

var app = builder.Build();

InitializeEncryption(app);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health/encryption", new HealthCheckOptions
{
    Predicate = check => check.Name == "encryption"
});

app.Run();

static void InitializeEncryption(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var serviceProvider = scope.ServiceProvider;
    var logger = serviceProvider.GetRequiredService<ILoggerFactory>()
        .CreateLogger("EncryptionStartup");

    try
    {
        var encryptionKeyProvider = serviceProvider.GetRequiredService<IEncryptionKeyProvider>();
        EncryptionHelper.Encryptor = new AesGcmStringEncryptor(encryptionKeyProvider);
        var key = encryptionKeyProvider.GetCurrentKey();
        logger.LogInformation("Encryption initialized with {ProviderName} and a {KeyLength}-byte key.",
            encryptionKeyProvider.GetType().Name,
            key.Length);
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "Failed to initialize encryption. Application cannot start without a valid key.");
        throw;
    }
}
