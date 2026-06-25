using Gatherstead.Api.Services.Accommodations;
using Gatherstead.Api.Tests.Fixtures;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Moq;

namespace Gatherstead.Api.Tests.Services;

public class AccommodationAvailabilityServiceTests : IAsyncLifetime
{
    private GathersteadDbContext _dbContext = null!;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _propertyId = Guid.NewGuid();
    private readonly Guid _capped = Guid.NewGuid();        // CapacityAdults=4, CapacityChildren=2
    private readonly Guid _uncapped = Guid.NewGuid();      // null capacity (unconstrained)
    private readonly Guid _householdId = Guid.NewGuid();
    private readonly Guid _member = Guid.NewGuid();

    private static readonly DateOnly Jun10 = new(2025, 6, 10);
    private static readonly DateOnly Jun12 = new(2025, 6, 12);
    private static readonly DateOnly Jun13 = new(2025, 6, 13);
    private static readonly DateOnly Jun14 = new(2025, 6, 14);

    public async ValueTask InitializeAsync()
    {
        _dbContext = TestDbContextFactory.Create(tenantId: _tenantId);
        _dbContext.Tenants.Add(new Tenant { Id = _tenantId, Name = "Test Tenant" });
        _dbContext.Properties.Add(new Property { Id = _propertyId, TenantId = _tenantId, Name = "Lake House" });
        _dbContext.Accommodations.Add(new Accommodation
        {
            Id = _capped, TenantId = _tenantId, PropertyId = _propertyId, Name = "Cabin A",
            Type = AccommodationType.Bedroom, CapacityAdults = 4, CapacityChildren = 2,
        });
        _dbContext.Accommodations.Add(new Accommodation
        {
            Id = _uncapped, TenantId = _tenantId, PropertyId = _propertyId, Name = "Open Field",
            Type = AccommodationType.Tent,
        });
        _dbContext.Households.Add(new Household { Id = _householdId, TenantId = _tenantId, Name = "Smith Household" });
        _dbContext.HouseholdMembers.Add(new HouseholdMember { Id = _member, TenantId = _tenantId, HouseholdId = _householdId, Name = "Alice" });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        _dbContext.Dispose();
        return ValueTask.CompletedTask;
    }

    private AccommodationAvailabilityService CreateService(Guid? contextTenantId = null)
    {
        var tenantContext = Mock.Of<ICurrentTenantContext>(c => c.TenantId == (contextTenantId ?? _tenantId));
        return new AccommodationAvailabilityService(_dbContext, tenantContext);
    }

    private async Task AddIntentAsync(Guid accommodationId, DateOnly start, DateOnly end, int? adults, int? children)
    {
        _dbContext.AccommodationIntents.Add(new AccommodationIntent
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, AccommodationId = accommodationId,
            HouseholdMemberId = _member, StartNight = start, EndNight = end,
            Status = AccommodationIntentStatus.Intent, PartyAdults = adults, PartyChildren = children,
        });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SearchAsync_InvertedSpan_ReturnsError()
    {
        var result = await CreateService().SearchAsync(_tenantId, Jun14, Jun10, 1, 0, true, TestContext.Current.CancellationToken);
        Assert.False(result.Successful);
    }

    [Fact]
    public async Task SearchAsync_NoIntents_ShowsFullRemainingCapacity()
    {
        var result = await CreateService().SearchAsync(_tenantId, Jun10, Jun12, 2, 1, true, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        var cabin = Assert.Single(result.Entity!, a => a.Id == _capped);
        Assert.Equal(0, cabin.ClaimedAdults);
        Assert.Equal(4, cabin.RemainingAdults);
        Assert.Equal(2, cabin.RemainingChildren);
        Assert.True(cabin.HasSufficientCapacity);
    }

    [Fact]
    public async Task SearchAsync_OverlappingIntents_ReduceRemainingCapacity()
    {
        await AddIntentAsync(_capped, Jun10, Jun12, adults: 3, children: 1);

        var result = await CreateService().SearchAsync(_tenantId, Jun10, Jun12, 1, 1, true, TestContext.Current.CancellationToken);

        var cabin = Assert.Single(result.Entity!, a => a.Id == _capped);
        Assert.Equal(3, cabin.ClaimedAdults);
        Assert.Equal(1, cabin.RemainingAdults);
        Assert.Equal(1, cabin.RemainingChildren);
        Assert.True(cabin.HasSufficientCapacity);
    }

    [Fact]
    public async Task SearchAsync_AdultsAndChildrenCheckedIndependently()
    {
        // Enough adult room (1 left) but children request exceeds remaining (1 left, asking 2).
        await AddIntentAsync(_capped, Jun10, Jun12, adults: 3, children: 1);

        var result = await CreateService().SearchAsync(_tenantId, Jun10, Jun12, 1, 2, requireCapacity: false, TestContext.Current.CancellationToken);

        var cabin = Assert.Single(result.Entity!, a => a.Id == _capped);
        Assert.False(cabin.HasSufficientCapacity);
    }

    [Fact]
    public async Task SearchAsync_NonOverlappingIntent_DoesNotConsumeCapacity()
    {
        // Stay [Jun13, Jun14] does not overlap the requested window [Jun10, Jun12].
        await AddIntentAsync(_capped, Jun13, Jun14, adults: 4, children: 2);

        var result = await CreateService().SearchAsync(_tenantId, Jun10, Jun12, 4, 2, true, TestContext.Current.CancellationToken);

        var cabin = Assert.Single(result.Entity!, a => a.Id == _capped);
        Assert.Equal(0, cabin.ClaimedAdults);
        Assert.True(cabin.HasSufficientCapacity);
    }

    [Fact]
    public async Task SearchAsync_TouchingNight_CountsAsOverlap()
    {
        // Stay [Jun12, Jun14] shares night Jun12 with the requested window [Jun10, Jun12].
        await AddIntentAsync(_capped, Jun12, Jun14, adults: 4, children: 0);

        var result = await CreateService().SearchAsync(_tenantId, Jun10, Jun12, 1, 0, requireCapacity: false, TestContext.Current.CancellationToken);

        var cabin = Assert.Single(result.Entity!, a => a.Id == _capped);
        Assert.Equal(4, cabin.ClaimedAdults);
        Assert.Equal(0, cabin.RemainingAdults);
        Assert.False(cabin.HasSufficientCapacity);
    }

    [Fact]
    public async Task SearchAsync_NullCapacity_IsAlwaysSufficient()
    {
        await AddIntentAsync(_uncapped, Jun10, Jun12, adults: 99, children: 99);

        var result = await CreateService().SearchAsync(_tenantId, Jun10, Jun12, 50, 50, true, TestContext.Current.CancellationToken);

        var tent = Assert.Single(result.Entity!, a => a.Id == _uncapped);
        Assert.Null(tent.RemainingAdults);
        Assert.True(tent.HasSufficientCapacity);
    }

    [Fact]
    public async Task SearchAsync_RequireCapacity_FiltersOutFullOptions()
    {
        await AddIntentAsync(_capped, Jun10, Jun12, adults: 4, children: 2);

        var filtered = await CreateService().SearchAsync(_tenantId, Jun10, Jun12, 1, 0, requireCapacity: true, TestContext.Current.CancellationToken);
        Assert.DoesNotContain(filtered.Entity!, a => a.Id == _capped);

        var all = await CreateService().SearchAsync(_tenantId, Jun10, Jun12, 1, 0, requireCapacity: false, TestContext.Current.CancellationToken);
        var cabin = Assert.Single(all.Entity!, a => a.Id == _capped);
        Assert.False(cabin.HasSufficientCapacity);
    }

    [Fact]
    public async Task SearchAsync_WrongTenantContext_ReturnsError()
    {
        var result = await CreateService(contextTenantId: Guid.NewGuid())
            .SearchAsync(_tenantId, Jun10, Jun12, 1, 0, true, TestContext.Current.CancellationToken);
        Assert.False(result.Successful);
    }
}
