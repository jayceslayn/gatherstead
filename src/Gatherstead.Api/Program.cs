using Gatherstead.Api.Encryption;
using Gatherstead.Api.Security;
using Gatherstead.Db;
using Gatherstead.Db.Encryption;
using Gatherstead.Db.Interceptors;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserContext, HttpContextCurrentUserContext>();
builder.Services.AddScoped<AuditingSaveChangesInterceptor>();

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

builder.Services.AddSingleton<IEncryptionKeyProvider, ConfigurationEncryptionKeyProvider>();

var app = builder.Build();

var encryptionKeyProvider = app.Services.GetRequiredService<IEncryptionKeyProvider>();
EncryptionHelper.Encryptor = new AesGcmStringEncryptor(encryptionKeyProvider);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
