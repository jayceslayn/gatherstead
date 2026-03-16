using Gatherstead.Api.Services;
using Gatherstead.Api.Services.Addresses;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.ContactMethods;
using Gatherstead.Api.Services.DietaryProfiles;
using Gatherstead.Api.Services.HouseholdMembers;
using Gatherstead.Api.Services.Households;
using Gatherstead.Api.Services.MemberAttributes;
using Gatherstead.Api.Services.MemberRelationships;
using Gatherstead.Api.Services.Tenants;
using Gatherstead.Api.Security;
using Gatherstead.Data;
using Gatherstead.Data.Interceptors;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserContext, HttpContextCurrentUserContext>();
builder.Services.AddScoped<ICurrentTenantContext, HttpContextCurrentTenantContext>();
builder.Services.AddScoped<IIncludeDeletedContext, HttpContextIncludeDeletedContext>();
builder.Services.AddScoped<AuditingSaveChangesInterceptor>();
builder.Services.AddScoped<IAppAdminContext, HttpContextAppAdminContext>();
builder.Services.AddScoped<IMemberAuthorizationService, MemberAuthorizationService>();
builder.Services.AddScoped<IAddressService, AddressService>();
builder.Services.AddScoped<IContactMethodService, ContactMethodService>();
builder.Services.AddScoped<IDietaryProfileService, DietaryProfileService>();
builder.Services.AddScoped<IHouseholdMemberService, HouseholdMemberService>();
builder.Services.AddScoped<IHouseholdService, HouseholdService>();
builder.Services.AddScoped<IMemberAttributeService, MemberAttributeService>();
builder.Services.AddScoped<IMemberRelationshipService, MemberRelationshipService>();
builder.Services.AddScoped<ITenantService, TenantService>();
builder.Services.AddScoped<ITokenRevocationService, TokenRevocationService>();

// Configure JWT Bearer authentication with external identity provider (Entra External ID / Azure AD B2C)
// Note: Consider PASETO migration when ecosystem support improves (broader library/provider adoption).
var identitySection = builder.Configuration.GetSection("ExternalIdentity");
var instance = identitySection["Instance"] ?? "";
var domain = identitySection["Domain"] ?? "";
var policyId = identitySection["SignUpSignInPolicyId"] ?? "";
var clientId = identitySection["ClientId"] ?? "";

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Authority URL format depends on the identity provider:
        //   Entra External ID:  {instance}/{domain}/v2.0  (no policy segment)
        //   Azure AD B2C:       {instance}/{domain}/{policyId}/v2.0
        options.Authority = string.IsNullOrEmpty(policyId)
            ? $"{instance}/{domain}/v2.0"
            : $"{instance}/{domain}/{policyId}/v2.0";
        options.Audience = clientId;

        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            // Issuer format: https://{instance}/{tenantId}/v2.0 — same for B2C and Entra External ID
            ValidIssuer = identitySection["ValidIssuer"],
            ValidAudience = clientId,
            ClockSkew = TimeSpan.FromMinutes(5),
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                // Check token revocation if jti claim is present
                var jti = context.Principal?.FindFirst("jti")?.Value;
                if (!string.IsNullOrEmpty(jti))
                {
                    var revocationService = context.HttpContext.RequestServices
                        .GetService<ITokenRevocationService>();
                    if (revocationService != null && await revocationService.IsTokenRevokedAsync(jti))
                    {
                        context.Fail("Token has been revoked.");
                    }
                }
            },
            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILogger<Program>>();
                logger.LogWarning(
                    "JWT authentication failed: {Message}, IP: {IpAddress}, UserAgent: {UserAgent}",
                    context.Exception.Message,
                    context.HttpContext.Connection.RemoteIpAddress,
                    context.Request.Headers.UserAgent.ToString()
                );
                return Task.CompletedTask;
            },
        };
    });

builder.Services.AddAuthorization();

// Configure HSTS
builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
});

// Configure CORS
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy
                .WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    });
});

// Add rate limiting services
var rateLimitingSection = builder.Configuration.GetSection("RateLimiting");
var permitLimit = rateLimitingSection.GetValue("PermitLimit", 100);
var windowMinutes = rateLimitingSection.GetValue("WindowMinutes", 1);
var queueLimit = rateLimitingSection.GetValue("QueueLimit", 0);

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
                PermitLimit = permitLimit,
                Window = TimeSpan.FromMinutes(windowMinutes),
                QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst,
                QueueLimit = queueLimit
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

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
    app.UseHsts();
}

// Security headers
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Content-Security-Policy", "default-src 'none'");
    context.Response.Headers.Append("Referrer-Policy", "no-referrer");
    context.Response.Headers.Append("Permissions-Policy", "geolocation=(), camera=(), microphone=()");
    await next();
});

app.UseCors();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
