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
    private readonly Guid _capped = Guid.NewGuid();        // 3 Queen beds → sleeps capacity 6
    private readonly Guid _uncapped = Guid.NewGuid();      // no beds → null capacity (unconstrained)
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
            Type = AccommodationType.Bedroom,
        });
        // 3 Queen beds → 3 × 2 = 6 sleeps.
        _dbContext.AccommodationBeds.Add(new AccommodationBed
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, AccommodationId = _capped, Size = BedSize.Queen, Quantity = 3,
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

    private async Task AddIntentAsync(
        Guid accommodationId, DateOnly start, DateOnly end, int? adults, int? children,
        AccommodationIntentStatus status = AccommodationIntentStatus.Requested)
    {
        _dbContext.AccommodationIntents.Add(new AccommodationIntent
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, AccommodationId = accommodationId,
            HouseholdMemberId = _member, StartNight = start, EndNight = end,
            Status = status, PartyAdults = adults, PartyChildren = children,
        });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SearchAsync_InvertedSpan_ReturnsError()
    {
        var result = await CreateService().SearchAsync(_tenantId, Jun14, Jun10, 1, 0, true, cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(result.Successful);
    }

    [Fact]
    public async Task SearchAsync_NoIntents_ShowsFullRemainingCapacity()
    {
        var result = await CreateService().SearchAsync(_tenantId, Jun10, Jun12, 2, 1, true, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        var cabin = Assert.Single(result.Entity!, a => a.Id == _capped);
        Assert.Equal(6, cabin.Capacity);
        Assert.Equal(0, cabin.Occupied);
        Assert.Equal(6, cabin.Remaining);
        Assert.True(cabin.HasSufficientCapacity);
    }

    [Fact]
    public async Task SearchAsync_OverlappingIntents_ReduceRemainingCapacity()
    {
        await AddIntentAsync(_capped, Jun10, Jun12, adults: 3, children: 1);

        var result = await CreateService().SearchAsync(_tenantId, Jun10, Jun12, 1, 1, true, cancellationToken: TestContext.Current.CancellationToken);

        var cabin = Assert.Single(result.Entity!, a => a.Id == _capped);
        Assert.Equal(4, cabin.Occupied);
        Assert.Equal(2, cabin.Remaining);
        Assert.True(cabin.HasSufficientCapacity);
    }

    [Fact]
    public async Task SearchAsync_PartyExceedingRemaining_IsInsufficient()
    {
        // Occupied 4 of 6 leaves 2; a party of 3 does not fit.
        await AddIntentAsync(_capped, Jun10, Jun12, adults: 3, children: 1);

        var result = await CreateService().SearchAsync(_tenantId, Jun10, Jun12, 2, 1, requireCapacity: false, cancellationToken: TestContext.Current.CancellationToken);

        var cabin = Assert.Single(result.Entity!, a => a.Id == _capped);
        Assert.Equal(2, cabin.Remaining);
        Assert.False(cabin.HasSufficientCapacity);
    }

    [Fact]
    public async Task SearchAsync_NonOverlappingIntent_DoesNotConsumeCapacity()
    {
        // Stay [Jun13, Jun14] does not overlap the requested window [Jun10, Jun12].
        await AddIntentAsync(_capped, Jun13, Jun14, adults: 4, children: 2);

        var result = await CreateService().SearchAsync(_tenantId, Jun10, Jun12, 4, 2, true, cancellationToken: TestContext.Current.CancellationToken);

        var cabin = Assert.Single(result.Entity!, a => a.Id == _capped);
        Assert.Equal(0, cabin.Occupied);
        Assert.True(cabin.HasSufficientCapacity);
    }

    [Fact]
    public async Task SearchAsync_DeclinedIntent_DoesNotConsumeCapacity()
    {
        // A declined stay overlapping the window must not count toward occupancy.
        await AddIntentAsync(_capped, Jun10, Jun12, adults: 6, children: 0, status: AccommodationIntentStatus.Declined);

        var result = await CreateService().SearchAsync(_tenantId, Jun10, Jun12, 4, 0, requireCapacity: true, cancellationToken: TestContext.Current.CancellationToken);

        var cabin = Assert.Single(result.Entity!, a => a.Id == _capped);
        Assert.Equal(0, cabin.Occupied);
        Assert.True(cabin.HasSufficientCapacity);
    }

    [Fact]
    public async Task SearchAsync_TouchingNight_CountsAsOverlap()
    {
        // Stay [Jun12, Jun14] shares night Jun12 with the requested window [Jun10, Jun12].
        await AddIntentAsync(_capped, Jun12, Jun14, adults: 6, children: 0);

        var result = await CreateService().SearchAsync(_tenantId, Jun10, Jun12, 1, 0, requireCapacity: false, cancellationToken: TestContext.Current.CancellationToken);

        var cabin = Assert.Single(result.Entity!, a => a.Id == _capped);
        Assert.Equal(6, cabin.Occupied);
        Assert.Equal(0, cabin.Remaining);
        Assert.False(cabin.HasSufficientCapacity);
    }

    [Fact]
    public async Task SearchAsync_NullCapacity_IsAlwaysSufficient()
    {
        await AddIntentAsync(_uncapped, Jun10, Jun12, adults: 99, children: 99);

        var result = await CreateService().SearchAsync(_tenantId, Jun10, Jun12, 50, 50, true, cancellationToken: TestContext.Current.CancellationToken);

        var tent = Assert.Single(result.Entity!, a => a.Id == _uncapped);
        Assert.Null(tent.Capacity);
        Assert.Null(tent.Remaining);
        Assert.True(tent.HasSufficientCapacity);
    }

    [Fact]
    public async Task SearchAsync_RequireCapacity_FiltersOutFullOptions()
    {
        await AddIntentAsync(_capped, Jun10, Jun12, adults: 6, children: 0);

        var filtered = await CreateService().SearchAsync(_tenantId, Jun10, Jun12, 1, 0, requireCapacity: true, cancellationToken: TestContext.Current.CancellationToken);
        Assert.DoesNotContain(filtered.Entity!, a => a.Id == _capped);

        var all = await CreateService().SearchAsync(_tenantId, Jun10, Jun12, 1, 0, requireCapacity: false, cancellationToken: TestContext.Current.CancellationToken);
        var cabin = Assert.Single(all.Entity!, a => a.Id == _capped);
        Assert.False(cabin.HasSufficientCapacity);
    }

    [Fact]
    public async Task SearchAsync_OrdersByTypeThenName()
    {
        // "Aardvark Camp" (Tent) sorts before "Cabin A" by name, but Bedroom precedes Tent by type,
        // so type ordering must win: Cabin A (Bedroom), then the tents by name.
        _dbContext.Accommodations.Add(new Accommodation
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, PropertyId = _propertyId,
            Name = "Aardvark Camp", Type = AccommodationType.Tent,
        });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await CreateService().SearchAsync(_tenantId, Jun10, Jun12, 1, 0, requireCapacity: false, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        Assert.Equal(
            ["Cabin A", "Aardvark Camp", "Open Field"],
            result.Entity!.Select(a => a.Name));
    }

    [Fact]
    public async Task SearchAsync_GroupsByPropertyThenTypeThenName()
    {
        // A second property whose name ("Alpine Lodge") sorts before "Lake House"; its accommodation
        // name ("Zed Suite") sorts last overall — so property-first grouping must lead with it.
        var otherPropertyId = Guid.NewGuid();
        _dbContext.Properties.Add(new Property { Id = otherPropertyId, TenantId = _tenantId, Name = "Alpine Lodge" });
        _dbContext.Accommodations.Add(new Accommodation
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, PropertyId = otherPropertyId,
            Name = "Zed Suite", Type = AccommodationType.Bedroom,
        });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await CreateService().SearchAsync(_tenantId, Jun10, Jun12, 1, 0, requireCapacity: false, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        Assert.Equal(
            ["Zed Suite", "Cabin A", "Open Field"],
            result.Entity!.Select(a => a.Name));
    }

    [Fact]
    public async Task SearchAsync_ScopedToSelectedProperties()
    {
        var otherPropertyId = Guid.NewGuid();
        var otherAccommodationId = Guid.NewGuid();
        _dbContext.Properties.Add(new Property { Id = otherPropertyId, TenantId = _tenantId, Name = "Alpine Lodge" });
        _dbContext.Accommodations.Add(new Accommodation
        {
            Id = otherAccommodationId, TenantId = _tenantId, PropertyId = otherPropertyId,
            Name = "Suite 1", Type = AccommodationType.Bedroom,
        });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await CreateService().SearchAsync(
            _tenantId, Jun10, Jun12, 1, 0, requireCapacity: false,
            propertyIds: [otherPropertyId], cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        var only = Assert.Single(result.Entity!);
        Assert.Equal(otherAccommodationId, only.Id);
    }

    [Fact]
    public async Task SearchAsync_EmptyPropertyIds_SearchesAllProperties()
    {
        var result = await CreateService().SearchAsync(
            _tenantId, Jun10, Jun12, 1, 0, requireCapacity: false,
            propertyIds: [], cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        // Both fixture accommodations under "Lake House" are returned when no property filter applies.
        Assert.Equal(2, result.Entity!.Count);
    }

    [Fact]
    public async Task SearchAsync_ScopedToSelectedTypes()
    {
        var result = await CreateService().SearchAsync(
            _tenantId, Jun10, Jun12, 1, 0, requireCapacity: false,
            types: [AccommodationType.Tent], cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        var only = Assert.Single(result.Entity!);
        Assert.Equal(_uncapped, only.Id);
    }

    [Fact]
    public async Task SearchAsync_EmptyTypes_SearchesAllTypes()
    {
        var result = await CreateService().SearchAsync(
            _tenantId, Jun10, Jun12, 1, 0, requireCapacity: false,
            types: [], cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        // Both fixture accommodations (a Bedroom and a Tent) are returned when no type filter applies.
        Assert.Equal(2, result.Entity!.Count);
    }

    [Fact]
    public async Task SearchAsync_WrongTenantContext_ReturnsError()
    {
        var result = await CreateService(contextTenantId: Guid.NewGuid())
            .SearchAsync(_tenantId, Jun10, Jun12, 1, 0, true, cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(result.Successful);
    }
}
