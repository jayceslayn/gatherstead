using Microsoft.Extensions.Caching.Hybrid;

namespace Gatherstead.Api.Security;

/// <summary>
/// Cross-request cache for frequently-accessed auth data (User mapping, App Admin flag, token
/// revocation, and tenant/household roles). Wraps <see cref="HybridCache"/> so it owns every cache
/// key and TTL in one place; callers supply a factory delegate that runs the DB query within their
/// own (scoped) <c>GathersteadDbContext</c>, keeping this a stateless singleton.
/// </summary>
/// <remarks>
/// L1 (in-process) only today. Registering an <c>IDistributedCache</c> (Redis) promotes it to an
/// L2 backplane automatically — no change here. Cached values are identifiers and role enums only,
/// never Always-Encrypted PII; role keys carry the tenant id so nothing crosses tenant boundaries.
/// </remarks>
public interface IAuthCache
{
    /// <summary>
    /// Resolves the internal user id for an external (Entra subject) id. A "not found" result is
    /// never cached, so a brand-new user provisioned mid-flight is not shadowed by a sticky null.
    /// </summary>
    Task<Guid?> GetUserIdAsync(string externalId, Func<CancellationToken, Task<Guid?>> factory, CancellationToken ct = default);

    /// <summary>Seeds the ExternalId → UserId mapping (called when a user row is first provisioned).</summary>
    Task SetUserIdAsync(string externalId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Evicts every entry keyed to a user — the ExternalId → UserId mapping, the app-admin flag, and
    /// the tenant/household role entries for each supplied tenant — so no cached authorization
    /// survives an account erasure. Owned here (next to the keys) so a new user-keyed dimension can
    /// never be silently missed by a caller.
    /// </summary>
    Task InvalidateAllForUserAsync(string externalId, Guid userId, IReadOnlyCollection<Guid> tenantIds, CancellationToken ct = default);

    Task<bool?> GetIsAppAdminAsync(Guid userId, Func<CancellationToken, Task<bool?>> factory, CancellationToken ct = default);

    Task<bool> GetIsRevokedAsync(string jti, Func<CancellationToken, Task<bool>> factory, CancellationToken ct = default);
    Task InvalidateRevokedAsync(string jti, CancellationToken ct = default);

    Task<T> GetTenantUserAsync<T>(Guid tenantId, Guid userId, Func<CancellationToken, Task<T>> factory, CancellationToken ct = default);
    Task InvalidateTenantUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default);

    Task<T> GetHouseholdUsersAsync<T>(Guid tenantId, Guid userId, Func<CancellationToken, Task<T>> factory, CancellationToken ct = default);
    Task InvalidateHouseholdUsersAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
}

/// <inheritdoc cref="IAuthCache"/>
public sealed class AuthCache : IAuthCache
{
    private readonly HybridCache _cache;
    private readonly HybridCacheEntryOptions _userOptions;
    private readonly HybridCacheEntryOptions _appAdminOptions;
    private readonly HybridCacheEntryOptions _revokedOptions;
    private readonly HybridCacheEntryOptions _roleOptions;

    public AuthCache(HybridCache cache, AuthCacheOptions options)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        ArgumentNullException.ThrowIfNull(options);
        _userOptions = Entry(TimeSpan.FromMinutes(options.UserMappingMinutes));
        _appAdminOptions = Entry(TimeSpan.FromMinutes(options.AppAdminMinutes));
        _revokedOptions = Entry(TimeSpan.FromSeconds(options.RevokedSeconds));
        _roleOptions = Entry(TimeSpan.FromSeconds(options.RoleSeconds));
    }

    // L1-only today: keep the distributed and local expirations identical so behaviour is unchanged
    // when an L2 backplane is added later.
    private static HybridCacheEntryOptions Entry(TimeSpan ttl) =>
        new() { Expiration = ttl, LocalCacheExpiration = ttl };

    private static string UserKey(string externalId) => $"auth:user:ext:{externalId}";
    private static string AppAdminKey(Guid userId) => $"auth:user:admin:{userId}";
    private static string RevokedKey(string jti) => $"auth:revoked:{jti}";
    private static string TenantUserKey(Guid tenantId, Guid userId) => $"auth:tenantuser:{tenantId}:{userId}";
    private static string HouseholdUsersKey(Guid tenantId, Guid userId) => $"auth:hhusers:{tenantId}:{userId}";

    public async Task<Guid?> GetUserIdAsync(string externalId, Func<CancellationToken, Task<Guid?>> factory, CancellationToken ct = default)
    {
        var key = UserKey(externalId);
        var id = await _cache.GetOrCreateAsync(key, factory, InvokeAsync, _userOptions, cancellationToken: ct);
        if (id is null)
        {
            // Don't let a "not found" stick — the row may be created by bootstrap moments later.
            await _cache.RemoveAsync(key, ct);
        }
        return id;
    }

    public Task SetUserIdAsync(string externalId, Guid userId, CancellationToken ct = default) =>
        _cache.SetAsync(UserKey(externalId), (Guid?)userId, _userOptions, cancellationToken: ct).AsTask();

    public async Task InvalidateAllForUserAsync(string externalId, Guid userId, IReadOnlyCollection<Guid> tenantIds, CancellationToken ct = default)
    {
        await _cache.RemoveAsync(UserKey(externalId), ct);
        await _cache.RemoveAsync(AppAdminKey(userId), ct);
        foreach (var tenantId in tenantIds)
        {
            await _cache.RemoveAsync(TenantUserKey(tenantId, userId), ct);
            await _cache.RemoveAsync(HouseholdUsersKey(tenantId, userId), ct);
        }
    }

    public async Task<bool?> GetIsAppAdminAsync(Guid userId, Func<CancellationToken, Task<bool?>> factory, CancellationToken ct = default) =>
        await _cache.GetOrCreateAsync(AppAdminKey(userId), factory, InvokeAsync, _appAdminOptions, cancellationToken: ct);

    public async Task<bool> GetIsRevokedAsync(string jti, Func<CancellationToken, Task<bool>> factory, CancellationToken ct = default) =>
        await _cache.GetOrCreateAsync(RevokedKey(jti), factory, InvokeAsync, _revokedOptions, cancellationToken: ct);

    public Task InvalidateRevokedAsync(string jti, CancellationToken ct = default) =>
        _cache.RemoveAsync(RevokedKey(jti), ct).AsTask();

    public async Task<T> GetTenantUserAsync<T>(Guid tenantId, Guid userId, Func<CancellationToken, Task<T>> factory, CancellationToken ct = default) =>
        await _cache.GetOrCreateAsync(TenantUserKey(tenantId, userId), factory, InvokeAsync, _roleOptions, cancellationToken: ct);

    public Task InvalidateTenantUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default) =>
        _cache.RemoveAsync(TenantUserKey(tenantId, userId), ct).AsTask();

    public async Task<T> GetHouseholdUsersAsync<T>(Guid tenantId, Guid userId, Func<CancellationToken, Task<T>> factory, CancellationToken ct = default) =>
        await _cache.GetOrCreateAsync(HouseholdUsersKey(tenantId, userId), factory, InvokeAsync, _roleOptions, cancellationToken: ct);

    public Task InvalidateHouseholdUsersAsync(Guid tenantId, Guid userId, CancellationToken ct = default) =>
        _cache.RemoveAsync(HouseholdUsersKey(tenantId, userId), ct).AsTask();

    // Passing the caller's factory as HybridCache state (rather than capturing it) keeps the
    // underlying factory delegate allocation-free per call.
    private static async ValueTask<T> InvokeAsync<T>(Func<CancellationToken, Task<T>> factory, CancellationToken ct) =>
        await factory(ct);
}
