using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Api.Services;

/// <summary>
/// Service for managing token revocation to invalidate tokens before their natural expiration
/// </summary>
public class TokenRevocationService : ITokenRevocationService
{
    private readonly GathersteadDbContext _dbContext;
    private readonly ILogger<TokenRevocationService> _logger;

    public TokenRevocationService(
        GathersteadDbContext dbContext,
        ILogger<TokenRevocationService> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task RevokeTokenAsync(
        string jti,
        Guid userId,
        Guid? tenantId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jti))
        {
            throw new ArgumentException("Token JTI cannot be null or empty", nameof(jti));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Revocation reason cannot be null or empty", nameof(reason));
        }

        // Check if token is already revoked
        var existingRevocation = await _dbContext.RevokedTokens
            .FirstOrDefaultAsync(rt => rt.Jti == jti, cancellationToken);

        if (existingRevocation != null)
        {
            _logger.LogWarning(
                "Token {Jti} is already revoked. UserId: {UserId}, TenantId: {TenantId}",
                jti, userId, tenantId);
            return;
        }

        var revokedToken = new RevokedToken
        {
            Id = Guid.NewGuid(),
            Jti = jti,
            UserId = userId,
            TenantId = tenantId,
            RevokedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(24), // Match max token age from configuration
            Reason = reason
        };

        _dbContext.RevokedTokens.Add(revokedToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Token revoked successfully. JTI: {Jti}, UserId: {UserId}, TenantId: {TenantId}, Reason: {Reason}",
            jti, userId, tenantId, reason);
    }

    public async Task<bool> IsTokenRevokedAsync(string jti, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jti))
        {
            return false;
        }

        return await _dbContext.RevokedTokens
            .AnyAsync(rt => rt.Jti == jti && rt.ExpiresAt > DateTime.UtcNow, cancellationToken);
    }

    public async Task CleanupExpiredRevocationsAsync(CancellationToken cancellationToken = default)
    {
        var expiredTokens = _dbContext.RevokedTokens
            .Where(rt => rt.ExpiresAt <= DateTime.UtcNow);

        var count = await expiredTokens.CountAsync(cancellationToken);

        if (count > 0)
        {
            _dbContext.RevokedTokens.RemoveRange(expiredTokens);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Cleaned up {Count} expired token revocations",
                count);
        }
    }
}
