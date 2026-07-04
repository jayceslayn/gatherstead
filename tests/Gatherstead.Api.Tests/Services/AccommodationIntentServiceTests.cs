using Gatherstead.Api.Contracts.AccommodationIntents;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Services.AccommodationIntents;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Tests.Fixtures;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Gatherstead.Api.Tests.Services;

public class AccommodationIntentServiceTests : IAsyncLifetime
{
    private GathersteadDbContext _dbContext = null!;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _propertyId = Guid.NewGuid();
    private readonly Guid _accommodationId = Guid.NewGuid();
    private readonly Guid _accommodationId2 = Guid.NewGuid();
    private readonly Guid _householdId = Guid.NewGuid();
    private readonly Guid _member = Guid.NewGuid();
    private readonly Guid _member2 = Guid.NewGuid();

    private static readonly DateOnly Day1 = new(2025, 6, 1);
    private static readonly DateOnly Day2 = new(2025, 6, 2);
    private static readonly DateOnly Day3 = new(2025, 6, 3);

    public async ValueTask InitializeAsync()
    {
        _dbContext = TestDbContextFactory.Create(tenantId: _tenantId);
        _dbContext.Tenants.Add(new Tenant { Id = _tenantId, Name = "Test Tenant" });
        _dbContext.Properties.Add(new Property { Id = _propertyId, TenantId = _tenantId, Name = "Lake House" });
        _dbContext.Accommodations.Add(new Accommodation
        {
            Id = _accommodationId,
            TenantId = _tenantId,
            PropertyId = _propertyId,
            Name = "Cabin A",
            Type = AccommodationType.Bedroom,
        });
        _dbContext.Accommodations.Add(new Accommodation
        {
            Id = _accommodationId2,
            TenantId = _tenantId,
            PropertyId = _propertyId,
            Name = "Cabin B",
            Type = AccommodationType.Bedroom,
        });
        _dbContext.Households.Add(new Household { Id = _householdId, TenantId = _tenantId, Name = "Smith Household" });
        _dbContext.HouseholdMembers.Add(new HouseholdMember { Id = _member, TenantId = _tenantId, HouseholdId = _householdId, Name = "Alice" });
        _dbContext.HouseholdMembers.Add(new HouseholdMember { Id = _member2, TenantId = _tenantId, HouseholdId = _householdId, Name = "Bob" });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        _dbContext.Dispose();
        return ValueTask.CompletedTask;
    }

    private AccommodationIntentService CreateService(bool canAssign = true)
    {
        var tenantContext = Mock.Of<ICurrentTenantContext>(c => c.TenantId == _tenantId);
        var auth = new Mock<IMemberAuthorizationService>();
        // Update/Delete authorize via CanAssignIntentForMemberAsync; Create classifies via ClassifyIntentActorAsync.
        auth.Setup(a => a.CanAssignIntentForMemberAsync(_tenantId, It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(canAssign);
        auth.Setup(a => a.ClassifyIntentActorAsync(_tenantId, It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(canAssign ? IntentSource.Volunteered : (IntentSource?)null);
        return new AccommodationIntentService(_dbContext, tenantContext, auth.Object, Mock.Of<IAuditVisibilityContext>());
    }

    private CreateAccommodationIntentRequest Request(DateOnly start, DateOnly end) => new()
    {
        HouseholdMemberId = _member,
        StartNight = start,
        EndNight = end,
        Status = AccommodationIntentStatus.Requested,
        PartyAdults = 2,
    };

    [Fact]
    public async Task CreateAsync_ValidSpan_PersistsStayAcrossNights()
    {
        var result = await CreateService()
            .CreateAsync(_tenantId, _accommodationId, _householdId, Request(Day1, Day3), TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        Assert.Equal(Day1, result.Entity!.StartNight);
        Assert.Equal(Day3, result.Entity.EndNight);
        Assert.Equal(2, result.Entity.PartyAdults);
    }

    [Fact]
    public async Task CreateAsync_InvertedSpan_ReturnsError()
    {
        var result = await CreateService()
            .CreateAsync(_tenantId, _accommodationId, _householdId, Request(Day3, Day1), TestContext.Current.CancellationToken);

        Assert.False(result.Successful);
        Assert.Contains(result.Messages, m => m.Type == MessageType.ERROR);
    }

    [Fact]
    public async Task CreateAsync_OverlappingStays_BothAllowed()
    {
        var service = CreateService();
        var first = await service.CreateAsync(_tenantId, _accommodationId, _householdId, Request(Day1, Day2), TestContext.Current.CancellationToken);
        // Same member, same accommodation, overlapping nights — capacity is a soft UI flag, not a rule.
        var second = await service.CreateAsync(_tenantId, _accommodationId, _householdId, Request(Day2, Day3), TestContext.Current.CancellationToken);

        Assert.True(first.Successful);
        Assert.True(second.Successful);
        var count = await _dbContext.AccommodationIntents
            .CountAsync(i => i.AccommodationId == _accommodationId, TestContext.Current.CancellationToken);
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task CreateAsync_Unauthorized_ReturnsError()
    {
        var result = await CreateService(canAssign: false)
            .CreateAsync(_tenantId, _accommodationId, _householdId, Request(Day1, Day2), TestContext.Current.CancellationToken);

        Assert.False(result.Successful);
    }

    [Fact]
    public async Task CreateAsync_MemberNotFound_ReturnsErrorNotServerError()
    {
        // A member id that doesn't belong to the household must be rejected up front rather than
        // hitting the FK constraint on save (which would surface as a 500).
        var request = new CreateAccommodationIntentRequest
        {
            HouseholdMemberId = Guid.NewGuid(),
            StartNight = Day1,
            EndNight = Day2,
            Status = AccommodationIntentStatus.Requested,
            PartyAdults = 1,
        };

        var result = await CreateService()
            .CreateAsync(_tenantId, _accommodationId, _householdId, request, TestContext.Current.CancellationToken);

        Assert.False(result.Successful);
        Assert.Contains(result.Messages, m => m.Type == MessageType.ERROR && m.Message.Contains("member", StringComparison.OrdinalIgnoreCase));
        Assert.False(await _dbContext.AccommodationIntents.AnyAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpdateAsync_PromotesStatus()
    {
        var service = CreateService();
        var created = await service.CreateAsync(_tenantId, _accommodationId, _householdId, Request(Day1, Day2), TestContext.Current.CancellationToken);

        var result = await service.UpdateAsync(_tenantId, _accommodationId, created.Entity!.Id, new UpdateAccommodationIntentRequest
        {
            HouseholdMemberId = _member,
            AccommodationId = _accommodationId,
            StartNight = Day1,
            EndNight = Day2,
            Status = AccommodationIntentStatus.Confirmed,
            PartyAdults = 2,
            PartyChildren = 1,
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        Assert.Equal(AccommodationIntentStatus.Confirmed, result.Entity!.Status);
        Assert.Equal(1, result.Entity.PartyChildren);
    }

    [Fact]
    public async Task UpdateAsync_DeclineSetsDeclinedStatus()
    {
        var service = CreateService();
        var created = await service.CreateAsync(_tenantId, _accommodationId, _householdId, Request(Day1, Day2), TestContext.Current.CancellationToken);

        var result = await service.UpdateAsync(_tenantId, _accommodationId, created.Entity!.Id, new UpdateAccommodationIntentRequest
        {
            HouseholdMemberId = _member,
            AccommodationId = _accommodationId,
            StartNight = Day1,
            EndNight = Day2,
            Status = AccommodationIntentStatus.Declined,
            PartyAdults = 2,
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        Assert.Equal(AccommodationIntentStatus.Declined, result.Entity!.Status);
    }

    [Fact]
    public async Task UpdateAsync_ReassignsMemberAndAccommodation()
    {
        var service = CreateService();
        var created = await service.CreateAsync(_tenantId, _accommodationId, _householdId, Request(Day1, Day2), TestContext.Current.CancellationToken);

        // Move the stay to a different member and accommodation; the route carries the current location.
        var result = await service.UpdateAsync(_tenantId, _accommodationId, created.Entity!.Id, new UpdateAccommodationIntentRequest
        {
            HouseholdMemberId = _member2,
            AccommodationId = _accommodationId2,
            StartNight = Day1,
            EndNight = Day2,
            Status = AccommodationIntentStatus.Requested,
            PartyAdults = 2,
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        Assert.Equal(_member2, result.Entity!.HouseholdMemberId);
        Assert.Equal(_accommodationId2, result.Entity.AccommodationId);

        var persisted = await _dbContext.AccommodationIntents
            .AsNoTracking()
            .SingleAsync(i => i.Id == created.Entity.Id, TestContext.Current.CancellationToken);
        Assert.Equal(_member2, persisted.HouseholdMemberId);
        Assert.Equal(_accommodationId2, persisted.AccommodationId);
    }

    [Fact]
    public async Task UpdateAsync_TargetAccommodationNotFound_ReturnsError()
    {
        var service = CreateService();
        var created = await service.CreateAsync(_tenantId, _accommodationId, _householdId, Request(Day1, Day2), TestContext.Current.CancellationToken);

        var result = await service.UpdateAsync(_tenantId, _accommodationId, created.Entity!.Id, new UpdateAccommodationIntentRequest
        {
            HouseholdMemberId = _member,
            AccommodationId = Guid.NewGuid(),
            StartNight = Day1,
            EndNight = Day2,
            Status = AccommodationIntentStatus.Requested,
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Successful);
        Assert.Contains(result.Messages, m => m.Type == MessageType.ERROR);
    }

    // ── ListForTenantAsync (cross-accommodation "my stays") ──────────────────

    [Fact]
    public async Task ListForTenantAsync_EnrichesWithAccommodationAndPropertyNames()
    {
        var service = CreateService();
        await service.CreateAsync(_tenantId, _accommodationId, _householdId, Request(Day1, Day2), TestContext.Current.CancellationToken);

        var result = await service.ListForTenantAsync(_tenantId, new[] { _member }, null, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        var stay = Assert.Single(result.Entity!);
        Assert.Equal("Cabin A", stay.AccommodationName);
        Assert.Equal("Lake House", stay.PropertyName);
        Assert.Equal(_propertyId, stay.PropertyId);
    }

    [Fact]
    public async Task ListForTenantAsync_MemberFilter_ExcludesOtherMembers()
    {
        var service = CreateService();
        await service.CreateAsync(_tenantId, _accommodationId, _householdId, Request(Day1, Day2), TestContext.Current.CancellationToken);

        var result = await service.ListForTenantAsync(_tenantId, new[] { _member2 }, null, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        Assert.Empty(result.Entity!);
    }

    [Fact]
    public async Task ListForTenantAsync_FromNight_ExcludesPastStays()
    {
        var service = CreateService();
        // A stay that has fully ended before the cutoff.
        await service.CreateAsync(_tenantId, _accommodationId, _householdId, Request(Day1, Day2), TestContext.Current.CancellationToken);

        var result = await service.ListForTenantAsync(_tenantId, new[] { _member }, Day3, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        Assert.Empty(result.Entity!);
    }

    // ── ListAsync (per-accommodation) ────────────────────────────────────────

    [Fact]
    public async Task ListAsync_ReturnsIntentsForAccommodation()
    {
        // Regression: the list projection must materialize before mapping, otherwise EF Core rejects
        // the instance MapToDto in the query shaper and the endpoint 500s.
        var service = CreateService();
        await service.CreateAsync(_tenantId, _accommodationId, _householdId, Request(Day1, Day2), TestContext.Current.CancellationToken);

        var result = await service.ListAsync(_tenantId, _accommodationId, null, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        var intent = Assert.Single(result.Entity!);
        Assert.Equal(_member, intent.HouseholdMemberId);
    }

    [Fact]
    public async Task ListAsync_MemberFilter_ExcludesOtherMembers()
    {
        var service = CreateService();
        await service.CreateAsync(_tenantId, _accommodationId, _householdId, Request(Day1, Day2), TestContext.Current.CancellationToken);

        var result = await service.ListAsync(_tenantId, _accommodationId, new[] { _member2 }, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        Assert.Empty(result.Entity!);
    }
}
