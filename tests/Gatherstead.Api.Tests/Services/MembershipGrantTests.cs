using Gatherstead.Api.Services.Membership;
using Gatherstead.Api.Tests.Fixtures;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Api.Tests.Services;

/// <summary>
/// <see cref="MembershipGrant.GrantAsync"/> runs on the tenant-less bootstrap claim path, so its
/// existence checks bypass the tenant query filter. These tests use a null tenant context (matching
/// production) and verify the bypass works: without it the checks would see nothing and re-insert a
/// duplicate, colliding on the composite primary key.
/// </summary>
public class MembershipGrantTests : IAsyncLifetime
{
    private GathersteadDbContext _dbContext = null!;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _householdId = Guid.NewGuid();

    public async ValueTask InitializeAsync()
    {
        // Null tenant context mirrors the bootstrap claim flow (runs before a tenant is resolved).
        _dbContext = TestDbContextFactory.Create(currentUserId: _userId);
        _dbContext.Tenants.Add(new Tenant { Id = _tenantId, Name = "Acme" });
        _dbContext.Users.Add(new User { Id = _userId, ExternalId = "user@test" });
        _dbContext.Households.Add(new Household { Id = _householdId, TenantId = _tenantId, Name = "House" });
        await _dbContext.SaveChangesAsync();
    }

    public ValueTask DisposeAsync()
    {
        _dbContext.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task GrantAsync_CreatesTenantUser_WhenAbsent()
    {
        var ct = TestContext.Current.CancellationToken;

        await MembershipGrant.GrantAsync(_dbContext, _tenantId, _userId, TenantRole.Owner, null, null, ct);
        await _dbContext.SaveChangesAsync(ct);

        var tu = await _dbContext.TenantUsers.IgnoreQueryFilters()
            .SingleAsync(x => x.TenantId == _tenantId && x.UserId == _userId, ct);
        Assert.Equal(TenantRole.Owner, tu.Role);
    }

    [Fact]
    public async Task GrantAsync_IsIdempotent_DespiteNullTenantContext()
    {
        var ct = TestContext.Current.CancellationToken;
        _dbContext.TenantUsers.Add(new TenantUser { TenantId = _tenantId, UserId = _userId, Role = TenantRole.Owner });
        await _dbContext.SaveChangesAsync(ct);

        // Re-grant at a lower role. The tenant-filter bypass lets the existence check find the row
        // even though the context tenant is null; without it this would insert a duplicate PK.
        await MembershipGrant.GrantAsync(_dbContext, _tenantId, _userId, TenantRole.Member, null, null, ct);
        await _dbContext.SaveChangesAsync(ct);

        var rows = await _dbContext.TenantUsers.IgnoreQueryFilters()
            .Where(x => x.TenantId == _tenantId && x.UserId == _userId)
            .ToListAsync(ct);
        var tu = Assert.Single(rows);
        Assert.Equal(TenantRole.Owner, tu.Role); // existing membership is never downgraded
    }

    [Fact]
    public async Task GrantAsync_CreatesHouseholdUser_WhenHouseholdProvided()
    {
        var ct = TestContext.Current.CancellationToken;

        await MembershipGrant.GrantAsync(
            _dbContext, _tenantId, _userId, TenantRole.Owner, _householdId, HouseholdRole.Manager, ct);
        await _dbContext.SaveChangesAsync(ct);

        var hu = await _dbContext.HouseholdUsers.IgnoreQueryFilters()
            .SingleAsync(x => x.HouseholdId == _householdId && x.UserId == _userId, ct);
        Assert.Equal(HouseholdRole.Manager, hu.Role);
    }

    [Fact]
    public async Task GrantAsync_IsIdempotent_ForHouseholdUser()
    {
        var ct = TestContext.Current.CancellationToken;
        _dbContext.TenantUsers.Add(new TenantUser { TenantId = _tenantId, UserId = _userId, Role = TenantRole.Owner });
        _dbContext.HouseholdUsers.Add(new HouseholdUser
        {
            TenantId = _tenantId, HouseholdId = _householdId, UserId = _userId, Role = HouseholdRole.Manager,
        });
        await _dbContext.SaveChangesAsync(ct);

        await MembershipGrant.GrantAsync(
            _dbContext, _tenantId, _userId, TenantRole.Owner, _householdId, HouseholdRole.Member, ct);
        await _dbContext.SaveChangesAsync(ct);

        var rows = await _dbContext.HouseholdUsers.IgnoreQueryFilters()
            .Where(x => x.HouseholdId == _householdId && x.UserId == _userId)
            .ToListAsync(ct);
        var hu = Assert.Single(rows);
        Assert.Equal(HouseholdRole.Manager, hu.Role); // existing household access is never downgraded
    }
}
