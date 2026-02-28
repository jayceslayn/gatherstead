using System.Security.Claims;
using System.Text.Encodings.Web;
using Gatherstead.Api.Security;
using Gatherstead.Api.Services;
using Gatherstead.Api.Tests.Fixtures;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Gatherstead.Api.Tests.Security;

public class PasetoAuthenticationHandlerTests
{
    private readonly PasetoTokenHelper _tokenHelper = new();

    private async Task<AuthenticateResult> AuthenticateAsync(
        string? authorizationHeader,
        Action<PasetoAuthenticationOptions>? configureOptions = null,
        ITokenRevocationService? revocationService = null,
        bool registerRevocationService = true)
    {
        var options = new PasetoAuthenticationOptions
        {
            PublicKeyBase64 = _tokenHelper.PublicKeyBase64,
            Audience = PasetoTokenHelper.TestAudience,
            Issuer = PasetoTokenHelper.TestIssuer,
            ValidateAudience = true,
            ValidateIssuer = true,
            RequireExpirationTime = true,
            ClockSkew = TimeSpan.FromMinutes(5),
            MaxTokenAge = TimeSpan.FromHours(24)
        };
        configureOptions?.Invoke(options);

        var optionsMonitor = new Mock<IOptionsMonitor<PasetoAuthenticationOptions>>();
        optionsMonitor.Setup(m => m.Get(PasetoAuthenticationDefaults.AuthenticationScheme)).Returns(options);

        var handler = new PasetoAuthenticationHandler(
            optionsMonitor.Object,
            NullLoggerFactory.Instance,
            UrlEncoder.Default);

        var httpContext = new DefaultHttpContext();
        if (authorizationHeader != null)
            httpContext.Request.Headers.Authorization = authorizationHeader;

        var services = new ServiceCollection();
        if (registerRevocationService)
        {
            services.AddSingleton(revocationService ?? Mock.Of<ITokenRevocationService>(
                s => s.IsTokenRevokedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()) == Task.FromResult(false)));
        }
        httpContext.RequestServices = services.BuildServiceProvider();

        var scheme = new AuthenticationScheme(
            PasetoAuthenticationDefaults.AuthenticationScheme,
            null,
            typeof(PasetoAuthenticationHandler));

        await handler.InitializeAsync(scheme, httpContext);
        return await handler.AuthenticateAsync();
    }

    [Fact]
    public async Task NoAuthorizationHeader_ReturnsNoResult()
    {
        var result = await AuthenticateAsync(null);

        Assert.False(result.Succeeded);
        Assert.True(result.None);
    }

    [Fact]
    public async Task EmptyAuthorizationHeader_ReturnsNoResult()
    {
        var result = await AuthenticateAsync("   ");

        Assert.False(result.Succeeded);
        Assert.True(result.None);
    }

    [Fact]
    public async Task NonBearerScheme_ReturnsNoResult()
    {
        var result = await AuthenticateAsync("Basic dGVzdDp0ZXN0");

        Assert.False(result.Succeeded);
        Assert.True(result.None);
    }

    [Fact]
    public async Task BearerWithNoToken_ReturnsNoResult()
    {
        var result = await AuthenticateAsync("Bearer    ");

        Assert.False(result.Succeeded);
        Assert.True(result.None);
    }

    [Fact]
    public async Task NullPublicKey_ReturnsFail()
    {
        var token = _tokenHelper.GenerateToken(sub: Guid.NewGuid().ToString());
        var result = await AuthenticateAsync(
            $"Bearer {token}",
            o => o.PublicKeyBase64 = null);

        Assert.False(result.Succeeded);
        Assert.Contains("not configured", result.Failure!.Message);
    }

    [Fact]
    public async Task ValidToken_ReturnsSuccess()
    {
        var userId = Guid.NewGuid().ToString();
        var token = _tokenHelper.GenerateToken(sub: userId, email: "test@example.com", name: "Test User");

        var result = await AuthenticateAsync($"Bearer {token}");

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Principal);
    }

    [Fact]
    public async Task ValidToken_MapsSubToNameIdentifier()
    {
        var userId = Guid.NewGuid().ToString();
        var token = _tokenHelper.GenerateToken(sub: userId);

        var result = await AuthenticateAsync($"Bearer {token}");

        Assert.True(result.Succeeded);
        var claim = result.Principal!.FindFirst(ClaimTypes.NameIdentifier);
        Assert.NotNull(claim);
        Assert.Equal(userId, claim.Value);
    }

    [Fact]
    public async Task ValidToken_MapsEmailClaim()
    {
        var token = _tokenHelper.GenerateToken(sub: Guid.NewGuid().ToString(), email: "user@example.com");

        var result = await AuthenticateAsync($"Bearer {token}");

        Assert.True(result.Succeeded);
        var claim = result.Principal!.FindFirst(ClaimTypes.Email);
        Assert.NotNull(claim);
        Assert.Equal("user@example.com", claim.Value);
    }

    [Fact]
    public async Task ValidToken_MapsNameClaim()
    {
        var token = _tokenHelper.GenerateToken(sub: Guid.NewGuid().ToString(), name: "John Doe");

        var result = await AuthenticateAsync($"Bearer {token}");

        Assert.True(result.Succeeded);
        var claim = result.Principal!.FindFirst(ClaimTypes.Name);
        Assert.NotNull(claim);
        Assert.Equal("John Doe", claim.Value);
    }

    [Fact]
    public async Task ValidToken_PreservesCustomClaims()
    {
        var token = _tokenHelper.GenerateToken(
            sub: Guid.NewGuid().ToString(),
            additionalClaims: new Dictionary<string, object> { ["tenant_id"] = "custom-value" });

        var result = await AuthenticateAsync($"Bearer {token}");

        Assert.True(result.Succeeded);
        var claim = result.Principal!.FindFirst("tenant_id");
        Assert.NotNull(claim);
        Assert.Equal("custom-value", claim.Value);
    }

    [Fact]
    public async Task InvalidSignature_ReturnsFail()
    {
        var token = _tokenHelper.GenerateTokenWithDifferentKey();

        var result = await AuthenticateAsync($"Bearer {token}");

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Failure);
    }

    [Fact]
    public async Task MalformedToken_ReturnsFail()
    {
        var result = await AuthenticateAsync("Bearer not-a-valid-paseto-token");

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Failure);
    }

    [Fact]
    public async Task TokenExceedsMaxAge_ReturnsFail()
    {
        var token = _tokenHelper.GenerateToken(
            sub: Guid.NewGuid().ToString(),
            iat: DateTime.UtcNow.AddHours(-25),
            exp: DateTime.UtcNow.AddHours(1));

        var result = await AuthenticateAsync($"Bearer {token}");

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Failure);
    }

    [Fact]
    public async Task FutureIssuedToken_BeyondClockSkew_ReturnsFail()
    {
        var token = _tokenHelper.GenerateToken(
            sub: Guid.NewGuid().ToString(),
            iat: DateTime.UtcNow.AddMinutes(10),
            exp: DateTime.UtcNow.AddHours(2));

        var result = await AuthenticateAsync($"Bearer {token}");

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Failure);
    }

    [Fact]
    public async Task RecentlyIssuedToken_WithinMaxAge_ReturnsSuccess()
    {
        // Token issued 1 hour ago with MaxTokenAge of 24 hours should succeed
        var token = _tokenHelper.GenerateToken(
            sub: Guid.NewGuid().ToString(),
            iat: DateTime.UtcNow.AddHours(-1),
            exp: DateTime.UtcNow.AddHours(1));

        var result = await AuthenticateAsync($"Bearer {token}");

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task RevokedToken_ReturnsFail()
    {
        var jti = Guid.NewGuid().ToString();
        var token = _tokenHelper.GenerateToken(sub: Guid.NewGuid().ToString(), jti: jti);

        var revocationService = Mock.Of<ITokenRevocationService>(
            s => s.IsTokenRevokedAsync(jti, It.IsAny<CancellationToken>()) == Task.FromResult(true));

        var result = await AuthenticateAsync($"Bearer {token}", revocationService: revocationService);

        Assert.False(result.Succeeded);
        Assert.Contains("revoked", result.Failure!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NoRevocationService_SkipsRevocationCheck()
    {
        var jti = Guid.NewGuid().ToString();
        var token = _tokenHelper.GenerateToken(sub: Guid.NewGuid().ToString(), jti: jti);

        var result = await AuthenticateAsync($"Bearer {token}", registerRevocationService: false);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task NoJtiClaim_SkipsRevocationCheck()
    {
        var token = _tokenHelper.GenerateToken(sub: Guid.NewGuid().ToString());

        var result = await AuthenticateAsync($"Bearer {token}");

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task ValidToken_AuthenticationSchemeIsCorrect()
    {
        var token = _tokenHelper.GenerateToken(sub: Guid.NewGuid().ToString());

        var result = await AuthenticateAsync($"Bearer {token}");

        Assert.True(result.Succeeded);
        Assert.Equal(PasetoAuthenticationDefaults.AuthenticationScheme,
            result.Ticket!.AuthenticationScheme);
    }
}
