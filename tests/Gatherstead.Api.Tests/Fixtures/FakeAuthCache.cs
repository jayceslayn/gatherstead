using Gatherstead.Api.Security;

namespace Gatherstead.Api.Tests.Fixtures;

/// <summary>
/// Pass-through <see cref="IAuthCache"/> for service unit tests: every read invokes the supplied
/// factory (no caching), so a service's behaviour is identical to the pre-cache implementation.
/// Invalidation and seed calls are recorded so tests can assert a write path evicts the right keys.
/// </summary>
public sealed class FakeAuthCache : IAuthCache
{
    public List<string> Invalidations { get; } = [];
    public List<(string ExternalId, Guid UserId)> Seeds { get; } = [];

    public Task<Guid?> GetUserIdAsync(string externalId, Func<CancellationToken, Task<Guid?>> factory, CancellationToken ct = default)
        => factory(ct);

    public Task SetUserIdAsync(string externalId, Guid userId, CancellationToken ct = default)
    {
        Seeds.Add((externalId, userId));
        return Task.CompletedTask;
    }

    public Task InvalidateAllForUserAsync(string externalId, Guid userId, IReadOnlyCollection<Guid> tenantIds, CancellationToken ct = default)
    {
        Invalidations.Add($"user:{externalId}");
        Invalidations.Add($"admin:{userId}");
        foreach (var tenantId in tenantIds)
        {
            Invalidations.Add($"tenantuser:{tenantId}:{userId}");
            Invalidations.Add($"hhusers:{tenantId}:{userId}");
        }
        return Task.CompletedTask;
    }

    public Task<bool?> GetIsAppAdminAsync(Guid userId, Func<CancellationToken, Task<bool?>> factory, CancellationToken ct = default)
        => factory(ct);

    public Task<bool> GetIsRevokedAsync(string jti, Func<CancellationToken, Task<bool>> factory, CancellationToken ct = default)
        => factory(ct);

    public Task InvalidateRevokedAsync(string jti, CancellationToken ct = default)
    {
        Invalidations.Add($"revoked:{jti}");
        return Task.CompletedTask;
    }

    public Task<T> GetTenantUserAsync<T>(Guid tenantId, Guid userId, Func<CancellationToken, Task<T>> factory, CancellationToken ct = default)
        => factory(ct);

    public Task InvalidateTenantUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        Invalidations.Add($"tenantuser:{tenantId}:{userId}");
        return Task.CompletedTask;
    }

    public Task<T> GetHouseholdUsersAsync<T>(Guid tenantId, Guid userId, Func<CancellationToken, Task<T>> factory, CancellationToken ct = default)
        => factory(ct);

    public Task InvalidateHouseholdUsersAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        Invalidations.Add($"hhusers:{tenantId}:{userId}");
        return Task.CompletedTask;
    }
}
