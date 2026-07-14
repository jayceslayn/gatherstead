using Gatherstead.Api.Security;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace Gatherstead.Api.Tests.Services;

/// <summary>
/// Exercises the real <see cref="AuthCache"/> over a real in-process <see cref="HybridCache"/> to
/// verify cache-aside hits, that "not found" user mappings are not cached, and that invalidation
/// removes the entry.
/// </summary>
public class AuthCacheTests
{
    private static AuthCache CreateCache()
    {
        var services = new ServiceCollection();
        services.AddHybridCache();
        var provider = services.BuildServiceProvider();
        var hybrid = provider.GetRequiredService<HybridCache>();
        return new AuthCache(hybrid, new AuthCacheOptions());
    }

    [Fact]
    public async Task GetIsRevokedAsync_CachesResult_FactoryRunsOnce()
    {
        var cache = CreateCache();
        var jti = Guid.NewGuid().ToString();
        var calls = 0;

        var first = await cache.GetIsRevokedAsync(
            jti, _ => { calls++; return Task.FromResult(false); }, TestContext.Current.CancellationToken);
        var second = await cache.GetIsRevokedAsync(
            jti, _ => { calls++; return Task.FromResult(false); }, TestContext.Current.CancellationToken);

        Assert.False(first);
        Assert.False(second);
        Assert.Equal(1, calls); // second call served from cache
    }

    [Fact]
    public async Task InvalidateRevokedAsync_ForcesFactoryRerun()
    {
        var cache = CreateCache();
        var jti = Guid.NewGuid().ToString();
        var revoked = false;

        var before = await cache.GetIsRevokedAsync(
            jti, _ => Task.FromResult(revoked), TestContext.Current.CancellationToken);
        Assert.False(before);

        // Token gets revoked; eviction must drop the cached "false".
        revoked = true;
        await cache.InvalidateRevokedAsync(jti, TestContext.Current.CancellationToken);

        var after = await cache.GetIsRevokedAsync(
            jti, _ => Task.FromResult(revoked), TestContext.Current.CancellationToken);
        Assert.True(after);
    }

    [Fact]
    public async Task GetUserIdAsync_FoundMapping_IsCached()
    {
        var cache = CreateCache();
        var externalId = Guid.NewGuid().ToString();
        var userId = Guid.NewGuid();
        var calls = 0;

        var first = await cache.GetUserIdAsync(
            externalId, _ => { calls++; return Task.FromResult<Guid?>(userId); }, TestContext.Current.CancellationToken);
        var second = await cache.GetUserIdAsync(
            externalId, _ => { calls++; return Task.FromResult<Guid?>(userId); }, TestContext.Current.CancellationToken);

        Assert.Equal(userId, first);
        Assert.Equal(userId, second);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task GetUserIdAsync_NotFound_IsNotCached()
    {
        var cache = CreateCache();
        var externalId = Guid.NewGuid().ToString();
        var newUserId = Guid.NewGuid();
        var provisioned = false;

        // First lookup: user not provisioned yet → null, and must NOT stick.
        var miss = await cache.GetUserIdAsync(
            externalId,
            _ => Task.FromResult<Guid?>(provisioned ? newUserId : null),
            TestContext.Current.CancellationToken);
        Assert.Null(miss);

        // User now exists; the next lookup must hit the DB factory again rather than a cached null.
        provisioned = true;
        var hit = await cache.GetUserIdAsync(
            externalId,
            _ => Task.FromResult<Guid?>(provisioned ? newUserId : null),
            TestContext.Current.CancellationToken);
        Assert.Equal(newUserId, hit);
    }

    [Fact]
    public async Task SetUserIdAsync_SeedsMapping_AvoidsFactory()
    {
        var cache = CreateCache();
        var externalId = Guid.NewGuid().ToString();
        var userId = Guid.NewGuid();

        await cache.SetUserIdAsync(externalId, userId, TestContext.Current.CancellationToken);

        var resolved = await cache.GetUserIdAsync(
            externalId,
            _ => throw new InvalidOperationException("factory should not run"),
            TestContext.Current.CancellationToken);
        Assert.Equal(userId, resolved);
    }

    [Fact]
    public async Task InvalidateTenantUserAsync_DropsCachedRole()
    {
        var cache = CreateCache();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var calls = 0;

        await cache.GetTenantUserAsync(
            tenantId, userId, _ => { calls++; return Task.FromResult("Member"); }, TestContext.Current.CancellationToken);
        await cache.InvalidateTenantUserAsync(tenantId, userId, TestContext.Current.CancellationToken);
        await cache.GetTenantUserAsync(
            tenantId, userId, _ => { calls++; return Task.FromResult("Owner"); }, TestContext.Current.CancellationToken);

        Assert.Equal(2, calls); // eviction forced the second factory run
    }
}
