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

    [Fact]
    public async Task GrantAsync_ReactivatesSoftDeletedTenantUser_AtNewRole()
    {
        var ct = TestContext.Current.CancellationToken;
        var tu = new TenantUser { TenantId = _tenantId, UserId = _userId, Role = TenantRole.Owner };
        _dbContext.TenantUsers.Add(tu);
        await _dbContext.SaveChangesAsync(ct);

        // Simulate a previously-removed membership.
        tu.IsDeleted = true;
        await _dbContext.SaveChangesAsync(ct);

        // Re-inviting: the soft-deleted row must be reactivated in place, not re-inserted (a fresh
        // insert would collide on the composite PK).
        await MembershipGrant.GrantAsync(_dbContext, _tenantId, _userId, TenantRole.Member, null, null, ct);
        await _dbContext.SaveChangesAsync(ct);

        var rows = await _dbContext.TenantUsers.IgnoreQueryFilters()
            .Where(x => x.TenantId == _tenantId && x.UserId == _userId)
            .ToListAsync(ct);
        var row = Assert.Single(rows);
        Assert.False(row.IsDeleted);
        Assert.Equal(TenantRole.Member, row.Role); // reactivation is a fresh grant at the invited role
    }

    [Fact]
    public async Task GrantAsync_ReactivatesSoftDeletedHouseholdUser()
    {
        var ct = TestContext.Current.CancellationToken;
        _dbContext.TenantUsers.Add(new TenantUser { TenantId = _tenantId, UserId = _userId, Role = TenantRole.Member });
        var hu = new HouseholdUser
        {
            TenantId = _tenantId, HouseholdId = _householdId, UserId = _userId, Role = HouseholdRole.Manager,
        };
        _dbContext.HouseholdUsers.Add(hu);
        await _dbContext.SaveChangesAsync(ct);

        hu.IsDeleted = true;
        await _dbContext.SaveChangesAsync(ct);

        await MembershipGrant.GrantAsync(
            _dbContext, _tenantId, _userId, TenantRole.Member, _householdId, HouseholdRole.Member, ct);
        await _dbContext.SaveChangesAsync(ct);

        var rows = await _dbContext.HouseholdUsers.IgnoreQueryFilters()
            .Where(x => x.HouseholdId == _householdId && x.UserId == _userId)
            .ToListAsync(ct);
        var row = Assert.Single(rows);
        Assert.False(row.IsDeleted);
        Assert.Equal(HouseholdRole.Member, row.Role);
    }

    [Fact]
    public async Task GrantAsync_LinksMember_WhenFree()
    {
        var ct = TestContext.Current.CancellationToken;
        var memberId = Guid.NewGuid();
        _dbContext.HouseholdMembers.Add(new HouseholdMember
        {
            Id = memberId, TenantId = _tenantId, HouseholdId = _householdId, Name = "Alice",
        });
        await _dbContext.SaveChangesAsync(ct);

        await MembershipGrant.GrantAsync(_dbContext, _tenantId, _userId, TenantRole.Member, null, null, ct, memberId);
        await _dbContext.SaveChangesAsync(ct);

        var tu = await _dbContext.TenantUsers.IgnoreQueryFilters()
            .SingleAsync(x => x.TenantId == _tenantId && x.UserId == _userId, ct);
        Assert.Equal(memberId, tu.LinkedMemberId);
    }

    [Fact]
    public async Task GrantAsync_AppliesLink_WhenMemberAlreadyLinkedToAnotherUser()
    {
        var ct = TestContext.Current.CancellationToken;
        var memberId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        _dbContext.HouseholdMembers.Add(new HouseholdMember
        {
            Id = memberId, TenantId = _tenantId, HouseholdId = _householdId, Name = "Alice",
        });
        _dbContext.Users.Add(new User { Id = otherUserId, ExternalId = "other@test" });
        _dbContext.TenantUsers.Add(new TenantUser
        {
            TenantId = _tenantId, UserId = otherUserId, Role = TenantRole.Member, LinkedMemberId = memberId,
        });
        await _dbContext.SaveChangesAsync(ct);

        // Another user already links the member — not a conflict; both links stand.
        await MembershipGrant.GrantAsync(_dbContext, _tenantId, _userId, TenantRole.Member, null, null, ct, memberId);
        await _dbContext.SaveChangesAsync(ct);

        var linked = await _dbContext.TenantUsers.IgnoreQueryFilters()
            .Where(x => x.TenantId == _tenantId && x.LinkedMemberId == memberId)
            .Select(x => x.UserId)
            .ToListAsync(ct);
        Assert.Equal(2, linked.Count);
        Assert.Contains(_userId, linked);
        Assert.Contains(otherUserId, linked);
    }

    [Fact]
    public async Task GrantAsync_NeverOverwritesExistingLink()
    {
        var ct = TestContext.Current.CancellationToken;
        var memberA = Guid.NewGuid();
        var memberB = Guid.NewGuid();
        _dbContext.HouseholdMembers.Add(new HouseholdMember
        {
            Id = memberA, TenantId = _tenantId, HouseholdId = _householdId, Name = "Alice",
        });
        _dbContext.HouseholdMembers.Add(new HouseholdMember
        {
            Id = memberB, TenantId = _tenantId, HouseholdId = _householdId, Name = "Bob",
        });
        _dbContext.TenantUsers.Add(new TenantUser
        {
            TenantId = _tenantId, UserId = _userId, Role = TenantRole.Member, LinkedMemberId = memberA,
        });
        await _dbContext.SaveChangesAsync(ct);

        // Re-inviting an already-linked user with a different (free) member must not silently
        // replace their established link and orphan the old member.
        await MembershipGrant.GrantAsync(_dbContext, _tenantId, _userId, TenantRole.Member, null, null, ct, memberB);
        await _dbContext.SaveChangesAsync(ct);

        var tu = await _dbContext.TenantUsers.IgnoreQueryFilters()
            .SingleAsync(x => x.TenantId == _tenantId && x.UserId == _userId, ct);
        Assert.Equal(memberA, tu.LinkedMemberId);
    }

    [Fact]
    public async Task GrantAsync_Reactivation_ReplacesStaleLink_WhenMemberInvited()
    {
        var ct = TestContext.Current.CancellationToken;
        var memberA = Guid.NewGuid();
        var memberB = Guid.NewGuid();
        _dbContext.HouseholdMembers.Add(new HouseholdMember
        {
            Id = memberA, TenantId = _tenantId, HouseholdId = _householdId, Name = "Alice",
        });
        _dbContext.HouseholdMembers.Add(new HouseholdMember
        {
            Id = memberB, TenantId = _tenantId, HouseholdId = _householdId, Name = "Bob",
        });
        _dbContext.TenantUsers.Add(new TenantUser
        {
            TenantId = _tenantId, UserId = _userId, Role = TenantRole.Member, LinkedMemberId = memberA, IsDeleted = true,
        });
        await _dbContext.SaveChangesAsync(ct);

        // A removed membership keeps its link, but that link is stale, not established — a re-invite
        // naming a different member must deliver the member it promised, not silently keep the old one.
        await MembershipGrant.GrantAsync(_dbContext, _tenantId, _userId, TenantRole.Member, null, null, ct, memberB);
        await _dbContext.SaveChangesAsync(ct);

        var tu = await _dbContext.TenantUsers.IgnoreQueryFilters()
            .SingleAsync(x => x.TenantId == _tenantId && x.UserId == _userId, ct);
        Assert.False(tu.IsDeleted);
        Assert.Equal(memberB, tu.LinkedMemberId);
    }

    [Fact]
    public async Task GrantAsync_Reactivation_KeepsPriorLink_WhenNoMemberInvited()
    {
        var ct = TestContext.Current.CancellationToken;
        var memberId = Guid.NewGuid();
        _dbContext.HouseholdMembers.Add(new HouseholdMember
        {
            Id = memberId, TenantId = _tenantId, HouseholdId = _householdId, Name = "Alice",
        });
        _dbContext.TenantUsers.Add(new TenantUser
        {
            TenantId = _tenantId, UserId = _userId, Role = TenantRole.Member, LinkedMemberId = memberId, IsDeleted = true,
        });
        await _dbContext.SaveChangesAsync(ct);

        // Re-inviting without a link restores the self-profile the user had before removal.
        await MembershipGrant.GrantAsync(_dbContext, _tenantId, _userId, TenantRole.Member, null, null, ct);
        await _dbContext.SaveChangesAsync(ct);

        var tu = await _dbContext.TenantUsers.IgnoreQueryFilters()
            .SingleAsync(x => x.TenantId == _tenantId && x.UserId == _userId, ct);
        Assert.False(tu.IsDeleted);
        Assert.Equal(memberId, tu.LinkedMemberId);
    }

    [Fact]
    public async Task GrantAsync_SkipsHouseholdGrant_WhenHouseholdDeleted()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await _dbContext.Households.IgnoreQueryFilters().SingleAsync(h => h.Id == _householdId, ct);
        household.IsDeleted = true;
        await _dbContext.SaveChangesAsync(ct);

        // The household was deleted between invite and accept: membership is still granted, but no
        // access row may be created for a household nothing can see.
        await MembershipGrant.GrantAsync(
            _dbContext, _tenantId, _userId, TenantRole.Member, _householdId, HouseholdRole.Member, ct);
        await _dbContext.SaveChangesAsync(ct);

        Assert.True(await _dbContext.TenantUsers.IgnoreQueryFilters()
            .AnyAsync(x => x.TenantId == _tenantId && x.UserId == _userId, ct));
        Assert.False(await _dbContext.HouseholdUsers.IgnoreQueryFilters().AnyAsync(ct));
    }
}
