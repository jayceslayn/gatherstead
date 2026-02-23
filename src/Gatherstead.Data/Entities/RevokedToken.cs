namespace Gatherstead.Data.Entities;

/// <summary>
/// Represents a revoked PASETO token that should no longer be accepted for authentication.
/// Used to invalidate tokens before their natural expiration (e.g., on logout, password change, or compromise).
/// </summary>
public class RevokedToken
{
    /// <summary>
    /// Primary key
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// JWT ID (jti claim) - unique identifier for the token
    /// </summary>
    public string Jti { get; set; } = null!;

    /// <summary>
    /// User who owned the token
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Tenant context (for multi-tenant isolation)
    /// </summary>
    public Guid? TenantId { get; set; }

    /// <summary>
    /// When the token was revoked
    /// </summary>
    public DateTime RevokedAt { get; set; }

    /// <summary>
    /// When the token expires (auto-cleanup after this time)
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Reason for revocation (logout, password change, admin action, compromise, etc.)
    /// </summary>
    public string Reason { get; set; } = null!;
}
