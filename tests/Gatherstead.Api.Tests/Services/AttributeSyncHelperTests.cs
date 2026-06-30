using Gatherstead.Api.Contracts.Attributes;
using Gatherstead.Api.Services.Attributes;
using Gatherstead.Api.Tests.Fixtures;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Api.Tests.Services;

/// <summary>
/// <see cref="AttributeSyncHelper.SyncAsync"/> loads existing rows with the soft-delete filter
/// bypassed so a re-used key re-activates the soft-deleted row instead of colliding on the unique
/// (TenantId, Key) index. Tenant isolation under that bypass is pinned generically by
/// <see cref="Data.QueryFilterCompositionTests"/>; these tests cover the helper's own semantics.
/// </summary>
public class AttributeSyncHelperTests : IAsyncLifetime
{
    private GathersteadDbContext _dbContext = null!;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public async ValueTask InitializeAsync()
    {
        _dbContext = TestDbContextFactory.Create(tenantId: _tenantId, currentUserId: _userId);
        _dbContext.Tenants.Add(new Tenant { Id = _tenantId, Name = "Acme" });
        _dbContext.Users.Add(new User { Id = _userId, ExternalId = "user@test" });
        await _dbContext.SaveChangesAsync();
    }

    public ValueTask DisposeAsync()
    {
        _dbContext.Dispose();
        return ValueTask.CompletedTask;
    }

    private IQueryable<TenantAttribute> ByParent()
        => _dbContext.TenantAttributes.Where(a => a.TenantId == _tenantId);

    private static AttributeWriteEntry Entry(string key, string value)
        => new() { Key = key, Value = value, TenantMinRole = (byte)TenantRole.Member };

    private Task SyncAsync(params AttributeWriteEntry[] incoming)
        => AttributeSyncHelper.SyncAsync(
            ByParent(),
            _dbContext.TenantAttributes,
            incoming,
            _ => true,
            _tenantId,
            () => new TenantAttribute(),
            applyExtra: null,
            TestContext.Current.CancellationToken);

    private async Task<TenantAttribute> SeedAttributeAsync(string key, string value, bool deleted)
    {
        var attr = new TenantAttribute { Id = Guid.NewGuid(), TenantId = _tenantId, Key = key, Value = value };
        _dbContext.TenantAttributes.Add(attr);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        if (deleted)
        {
            attr.IsDeleted = true;
            await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        return attr;
    }

    [Fact]
    public async Task SyncAsync_ReactivatesSoftDeletedAttribute_ByKey()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedAttributeAsync("color", "old", deleted: true);

        await SyncAsync(Entry("color", "blue"));
        await _dbContext.SaveChangesAsync(ct);

        // The soft-deleted row is revived in place — exactly one physical row, no unique-index collision.
        var rows = await _dbContext.TenantAttributes.IgnoreQueryFilters()
            .Where(a => a.TenantId == _tenantId && a.Key == "color").ToListAsync(ct);
        var attr = Assert.Single(rows);
        Assert.False(attr.IsDeleted);
        Assert.Equal("blue", attr.Value);
    }

    [Fact]
    public async Task SyncAsync_AddsNewAttribute_WhenAbsent()
    {
        var ct = TestContext.Current.CancellationToken;

        await SyncAsync(Entry("color", "blue"));
        await _dbContext.SaveChangesAsync(ct);

        var attr = await _dbContext.TenantAttributes.SingleAsync(a => a.Key == "color", ct);
        Assert.Equal("blue", attr.Value);
    }

    [Fact]
    public async Task SyncAsync_SoftDeletesVisibleAttribute_AbsentFromIncoming()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedAttributeAsync("color", "blue", deleted: false);

        await SyncAsync(); // full replace with an empty set
        await _dbContext.SaveChangesAsync(ct);

        Assert.Empty(await _dbContext.TenantAttributes.Where(a => a.Key == "color").ToListAsync(ct));
        var row = await _dbContext.TenantAttributes.IgnoreQueryFilters().SingleAsync(a => a.Key == "color", ct);
        Assert.True(row.IsDeleted);
    }
}
