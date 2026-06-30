namespace Gatherstead.Api.Security;

/// <summary>
/// TTLs for the cross-request auth caches, bound from the "AuthCache" configuration section.
/// Defaults are conservative: identifiers that never change are cached for longer, while
/// security-sensitive lookups (revocation, roles) use a short window so a missed invalidation
/// self-heals quickly. See <see cref="AuthCache"/>.
/// </summary>
public sealed class AuthCacheOptions
{
    /// <summary>ExternalId → internal UserId. Effectively immutable once provisioned.</summary>
    public int UserMappingMinutes { get; set; } = 60;

    /// <summary><see cref="Gatherstead.Data.Entities.User.IsAppAdmin"/>. Changes rarely (SQL today).</summary>
    public int AppAdminMinutes { get; set; } = 5;

    /// <summary>Token revocation check. Bounds how long a revoked token can still be accepted.</summary>
    public int RevokedSeconds { get; set; } = 60;

    /// <summary>TenantUser / HouseholdUser role lookups. Backstop for any missed eviction.</summary>
    public int RoleSeconds { get; set; } = 60;
}
