using Gatherstead.Api.Services;
using Gatherstead.Api.Tests.Fixtures;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace Gatherstead.Api.Tests.Services;

public class TokenRevocationServiceTests : IDisposable
{
    private readonly Gatherstead.Data.GathersteadDbContext _dbContext;
    private readonly TokenRevocationService _service;

    public TokenRevocationServiceTests()
    {
        _dbContext = TestDbContextFactory.Create();
        var logger = Mock.Of<ILogger<TokenRevocationService>>();
        _service = new TokenRevocationService(_dbContext, logger);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task RevokeTokenAsync_CreatesRecord()
    {
        var jti = Guid.NewGuid().ToString();
        var userId = Guid.NewGuid();

        await _service.RevokeTokenAsync(jti, userId, null, "User logout", TestContext.Current.CancellationToken);

        var revoked = await _dbContext.RevokedTokens.FirstOrDefaultAsync(r => r.Jti == jti, TestContext.Current.CancellationToken);
        Assert.NotNull(revoked);
        Assert.Equal(userId, revoked.UserId);
        Assert.Equal("User logout", revoked.Reason);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RevokeTokenAsync_NullOrEmptyJti_ThrowsArgumentException(string? jti)
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.RevokeTokenAsync(jti!, Guid.NewGuid(), null, "reason", TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RevokeTokenAsync_NullOrEmptyReason_ThrowsArgumentException(string? reason)
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.RevokeTokenAsync("jti-123", Guid.NewGuid(), null, reason!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RevokeTokenAsync_AlreadyRevoked_DoesNotDuplicate()
    {
        var jti = Guid.NewGuid().ToString();
        var userId = Guid.NewGuid();

        await _service.RevokeTokenAsync(jti, userId, null, "First revocation", TestContext.Current.CancellationToken);
        await _service.RevokeTokenAsync(jti, userId, null, "Second revocation", TestContext.Current.CancellationToken);

        var count = await _dbContext.RevokedTokens.CountAsync(r => r.Jti == jti, TestContext.Current.CancellationToken);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task IsTokenRevokedAsync_RevokedToken_ReturnsTrue()
    {
        var jti = Guid.NewGuid().ToString();
        await _service.RevokeTokenAsync(jti, Guid.NewGuid(), null, "Revoked", TestContext.Current.CancellationToken);

        var result = await _service.IsTokenRevokedAsync(jti, TestContext.Current.CancellationToken);

        Assert.True(result);
    }

    [Fact]
    public async Task IsTokenRevokedAsync_NotRevoked_ReturnsFalse()
    {
        var result = await _service.IsTokenRevokedAsync("nonexistent-jti", TestContext.Current.CancellationToken);

        Assert.False(result);
    }

    [Fact]
    public async Task IsTokenRevokedAsync_ExpiredRevocation_ReturnsFalse()
    {
        var jti = Guid.NewGuid().ToString();
        _dbContext.RevokedTokens.Add(new RevokedToken
        {
            Id = Guid.NewGuid(),
            Jti = jti,
            UserId = Guid.NewGuid(),
            RevokedAt = DateTime.UtcNow.AddHours(-25),
            ExpiresAt = DateTime.UtcNow.AddHours(-1),
            Reason = "Expired"
        });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.IsTokenRevokedAsync(jti, TestContext.Current.CancellationToken);

        Assert.False(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task IsTokenRevokedAsync_NullOrEmptyJti_ReturnsFalse(string? jti)
    {
        var result = await _service.IsTokenRevokedAsync(jti!, TestContext.Current.CancellationToken);

        Assert.False(result);
    }

    [Fact]
    public async Task CleanupExpiredRevocationsAsync_RemovesExpired()
    {
        _dbContext.RevokedTokens.Add(new RevokedToken
        {
            Id = Guid.NewGuid(),
            Jti = "expired-jti",
            UserId = Guid.NewGuid(),
            RevokedAt = DateTime.UtcNow.AddHours(-25),
            ExpiresAt = DateTime.UtcNow.AddHours(-1),
            Reason = "Expired"
        });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _service.CleanupExpiredRevocationsAsync(TestContext.Current.CancellationToken);

        var count = await _dbContext.RevokedTokens.CountAsync(r => r.Jti == "expired-jti", TestContext.Current.CancellationToken);
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task CleanupExpiredRevocationsAsync_KeepsActive()
    {
        var jti = Guid.NewGuid().ToString();
        _dbContext.RevokedTokens.Add(new RevokedToken
        {
            Id = Guid.NewGuid(),
            Jti = jti,
            UserId = Guid.NewGuid(),
            RevokedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(23),
            Reason = "Active"
        });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _service.CleanupExpiredRevocationsAsync(TestContext.Current.CancellationToken);

        var count = await _dbContext.RevokedTokens.CountAsync(r => r.Jti == jti, TestContext.Current.CancellationToken);
        Assert.Equal(1, count);
    }

    [Fact]
    public void Constructor_NullDbContext_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => new TokenRevocationService(null!, Mock.Of<ILogger<TokenRevocationService>>()));
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => new TokenRevocationService(_dbContext, null!));
    }
}
