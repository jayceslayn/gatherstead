namespace Gatherstead.Api.Services;

/// <summary>
/// Service for managing token revocation to invalidate tokens before their natural expiration
/// </summary>
public interface ITokenRevocationService
{
    /// <summary>
    /// Revokes a token by its JWT ID (jti claim)
    /// </summary>
    /// <param name="jti">The unique token identifier from the jti claim</param>
    /// <param name="userId">The user who owns the token</param>
    /// <param name="tenantId">The tenant context for multi-tenant isolation</param>
    /// <param name="reason">Reason for revocation (e.g., "User logout", "Password change", "Admin action")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task RevokeTokenAsync(string jti, Guid userId, Guid? tenantId, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a token has been revoked
    /// </summary>
    /// <param name="jti">The unique token identifier from the jti claim</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the token has been revoked and is still within its expiration window</returns>
    Task<bool> IsTokenRevokedAsync(string jti, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes expired token revocations from the database to prevent unbounded growth
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task CleanupExpiredRevocationsAsync(CancellationToken cancellationToken = default);
}
