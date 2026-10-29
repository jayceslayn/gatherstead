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
    private readonly Guid _householdId2 = Guid.NewGuid();
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
        _dbContext.Households.Add(new Household { Id = _householdId2, TenantId = _tenantId, Name = "Jones Household" });
        _dbContext.HouseholdMembers.Add(new HouseholdMember { Id = _member, TenantId = _tenantId, HouseholdId = _householdId, Name = "Alice" });
        _dbContext.HouseholdMembers.Add(new HouseholdMember { Id = _member2, TenantId = _tenantId, HouseholdId = _householdId2, Name = "Bob" });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        _dbContext.Dispose();
        return ValueTask.CompletedTask;
    }

    private AccommodationIntentService CreateService(
        bool canAssign = true,
        bool canManageEvent = false,
        Func<Guid, Guid, IntentSource?>? classify = null)
    {
        return CreateService(CreateAuthorization(canAssign, canManageEvent, classify));
    }

    private AccommodationIntentService CreateService(Mock<IMemberAuthorizationService> auth)
    {
        var tenantContext = Mock.Of<ICurrentTenantContext>(c => c.TenantId == _tenantId);
        return new AccommodationIntentService(_dbContext, tenantContext, auth.Object, Mock.Of<IAuditVisibilityContext>());
    }

    private Mock<IMemberAuthorizationService> CreateAuthorization(
        bool canAssign = true,
        bool canManageEvent = false,
        Func<Guid, Guid, IntentSource?>? classify = null)
    {
        var auth = new Mock<IMemberAuthorizationService>();
        auth.Setup(a => a.CanAssignIntentForMemberAsync(_tenantId, It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(canAssign);
        auth.Setup(a => a.ClassifyIntentActorAsync(_tenantId, It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid _, Guid householdId, Guid memberId, CancellationToken _) =>
            {
                var source = classify is null
                    ? canAssign ? IntentSource.Volunteered : (IntentSource?)null
                    : classify(householdId, memberId);
                return Task.FromResult(source);
            });
        auth.Setup(a => a.CanManageEventAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(canManageEvent);
        return auth;
    }

    private CreateAccommodationIntentRequest Request(DateOnly start, DateOnly end) => new()
    {
        HouseholdMemberId = _member,
        StartNight = start,
        EndNight = end,
        Status = AccommodationIntentStatus.Requested,
        PartyAdults = 2,
    };

    private async Task<AccommodationIntent> SeedStayAsync(
        Guid memberId,
        AccommodationIntentStatus status = AccommodationIntentStatus.Requested)
    {
        var stay = new AccommodationIntent
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            AccommodationId = _accommodationId,
            HouseholdMemberId = memberId,
            StartNight = Day1,
            EndNight = Day2,
            Status = status,
            Notes = "Original notes",
            PartyAdults = 2,
        };
        _dbContext.AccommodationIntents.Add(stay);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        return stay;
    }

    private UpdateAccommodationIntentRequest UpdateRequest(
        Guid memberId,
        AccommodationIntentStatus status = AccommodationIntentStatus.Requested,
        Guid? accommodationId = null,
        DateOnly? startNight = null,
        DateOnly? endNight = null) => new()
    {
        HouseholdMemberId = memberId,
        AccommodationId = accommodationId ?? _accommodationId,
        StartNight = startNight ?? Day1,
        EndNight = endNight ?? Day2,
        Status = status,
        Notes = "Updated notes",
        PartyAdults = 3,
        PartyChildren = 1,
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
    public async Task CreateAsync_SameSpanTwice_UpsertsSingleRow()
    {
        // A true duplicate (identical member + accommodation + span) must not create a second row —
        // the second create upserts the existing one, backed by the unique index.
        var service = CreateService();
        var first = await service.CreateAsync(_tenantId, _accommodationId, _householdId, Request(Day1, Day2), TestContext.Current.CancellationToken);
        var second = await service.CreateAsync(_tenantId, _accommodationId, _householdId, Request(Day1, Day2), TestContext.Current.CancellationToken);

        Assert.True(first.Successful);
        Assert.True(second.Successful);
        var count = await _dbContext.AccommodationIntents
            .CountAsync(i => i.AccommodationId == _accommodationId && i.HouseholdMemberId == _member, TestContext.Current.CancellationToken);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task CreateAsync_AfterDelete_RevivesRow()
    {
        // Re-requesting a withdrawn stay revives the soft-deleted row rather than colliding with the
        // unique index (which would surface as a 500).
        var service = CreateService();
        var created = await service.CreateAsync(_tenantId, _accommodationId, _householdId, Request(Day1, Day2), TestContext.Current.CancellationToken);
        await service.DeleteAsync(_tenantId, _accommodationId, created.Entity!.Id, TestContext.Current.CancellationToken);

        var revived = await service.CreateAsync(_tenantId, _accommodationId, _householdId, Request(Day1, Day2), TestContext.Current.CancellationToken);

        Assert.True(revived.Successful);
        var liveCount = await _dbContext.AccommodationIntents
            .CountAsync(i => i.AccommodationId == _accommodationId && i.HouseholdMemberId == _member, TestContext.Current.CancellationToken);
        Assert.Equal(1, liveCount);
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

    [Theory]
    [InlineData(AccommodationIntentStatus.Hold)]
    [InlineData(AccommodationIntentStatus.Confirmed)]
    [InlineData(AccommodationIntentStatus.Declined)]
    public async Task CreateAsync_NonCoordinatorCannotCreateManagedStatus(AccommodationIntentStatus status)
    {
        var request = Request(Day1, Day2);
        request = new CreateAccommodationIntentRequest
        {
            HouseholdMemberId = request.HouseholdMemberId,
            StartNight = request.StartNight,
            EndNight = request.EndNight,
            Status = status,
            PartyAdults = request.PartyAdults,
        };

        var result = await CreateService()
            .CreateAsync(_tenantId, _accommodationId, _householdId, request, TestContext.Current.CancellationToken);

        Assert.False(result.Successful);
        Assert.Contains(result.Messages, m => m.Type == MessageType.ERROR);
        Assert.False(await _dbContext.AccommodationIntents.AnyAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateAsync_UndefinedStatus_ReturnsError()
    {
        var request = new CreateAccommodationIntentRequest
        {
            HouseholdMemberId = _member,
            StartNight = Day1,
            EndNight = Day2,
            Status = (AccommodationIntentStatus)999,
        };

        var result = await CreateService()
            .CreateAsync(_tenantId, _accommodationId, _householdId, request, TestContext.Current.CancellationToken);

        Assert.False(result.Successful);
        Assert.Contains(result.Messages, m => m.Type == MessageType.ERROR);
        Assert.False(await _dbContext.AccommodationIntents.AnyAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateAsync_CoordinatorCanCreateManagedStatus()
    {
        var request = Request(Day1, Day2);
        request = new CreateAccommodationIntentRequest
        {
            HouseholdMemberId = request.HouseholdMemberId,
            StartNight = request.StartNight,
            EndNight = request.EndNight,
            Status = AccommodationIntentStatus.Hold,
            PartyAdults = request.PartyAdults,
        };

        var result = await CreateService(canManageEvent: true)
            .CreateAsync(_tenantId, _accommodationId, _householdId, request, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        Assert.Equal(AccommodationIntentStatus.Hold, result.Entity!.Status);
    }

    [Fact]
    public async Task CreateAsync_NonCoordinatorCannotUpsertExistingManagedStatus()
    {
        var stay = await SeedStayAsync(_member, AccommodationIntentStatus.Hold);

        var result = await CreateService()
            .CreateAsync(_tenantId, _accommodationId, _householdId, Request(stay.StartNight, stay.EndNight), TestContext.Current.CancellationToken);

        Assert.False(result.Successful);
        var persisted = await _dbContext.AccommodationIntents.AsNoTracking()
            .SingleAsync(i => i.Id == stay.Id, TestContext.Current.CancellationToken);
        Assert.Equal(AccommodationIntentStatus.Hold, persisted.Status);
        Assert.Equal("Original notes", persisted.Notes);
    }

    [Fact]
    public async Task UpdateAsync_PromotesStatus()
    {
        var service = CreateService(canManageEvent: true);
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
        var service = CreateService(canManageEvent: true);
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
        var service = CreateService(canManageEvent: true);
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
    public async Task UpdateAsync_CannotTakeOverAnotherMembersStayByRequestingOwnMemberId()
    {
        var stay = await SeedStayAsync(_member);
        var auth = CreateAuthorization(classify: (householdId, memberId) =>
            householdId == _householdId2 && memberId == _member2 ? IntentSource.Volunteered : null);

        var result = await CreateService(auth)
            .UpdateAsync(_tenantId, _accommodationId, stay.Id, UpdateRequest(_member2), TestContext.Current.CancellationToken);

        Assert.False(result.Successful);
        var persisted = await _dbContext.AccommodationIntents.AsNoTracking()
            .SingleAsync(i => i.Id == stay.Id, TestContext.Current.CancellationToken);
        Assert.Equal(_member, persisted.HouseholdMemberId);
        Assert.Equal(AccommodationIntentStatus.Requested, persisted.Status);
        Assert.Equal("Original notes", persisted.Notes);
        auth.Verify(a => a.ClassifyIntentActorAsync(_tenantId, _householdId, _member, It.IsAny<CancellationToken>()), Times.Once);
        auth.Verify(a => a.ClassifyIntentActorAsync(_tenantId, _householdId2, _member2, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(AccommodationIntentStatus.Hold)]
    [InlineData(AccommodationIntentStatus.Confirmed)]
    [InlineData(AccommodationIntentStatus.Declined)]
    public async Task UpdateAsync_NonCoordinatorCannotSetManagedStatus(AccommodationIntentStatus status)
    {
        var stay = await SeedStayAsync(_member);

        var result = await CreateService()
            .UpdateAsync(_tenantId, _accommodationId, stay.Id, UpdateRequest(_member, status), TestContext.Current.CancellationToken);

        Assert.False(result.Successful);
        var persisted = await _dbContext.AccommodationIntents.AsNoTracking()
            .SingleAsync(i => i.Id == stay.Id, TestContext.Current.CancellationToken);
        Assert.Equal(AccommodationIntentStatus.Requested, persisted.Status);
        Assert.Equal("Original notes", persisted.Notes);
        Assert.Equal(2, persisted.PartyAdults);
    }

    [Fact]
    public async Task UpdateAsync_NonCoordinatorCanEditOwnRequestedStay()
    {
        var stay = await SeedStayAsync(_member);

        var result = await CreateService()
            .UpdateAsync(_tenantId, _accommodationId, stay.Id, UpdateRequest(_member, startNight: Day2, endNight: Day3), TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        Assert.Equal(AccommodationIntentStatus.Requested, result.Entity!.Status);
        Assert.Equal(Day2, result.Entity.StartNight);
        Assert.Equal(Day3, result.Entity.EndNight);
        Assert.Equal("Updated notes", result.Entity.Notes);
        Assert.Equal(3, result.Entity.PartyAdults);
        Assert.Equal(1, result.Entity.PartyChildren);
    }

    [Fact]
    public async Task UpdateAsync_HouseholdManagerUsesActualHouseholdIdForRequestedStay()
    {
        var stay = await SeedStayAsync(_member);
        var auth = CreateAuthorization(
            canAssign: false,
            classify: (householdId, memberId) => householdId == _householdId && memberId == _member
                ? IntentSource.Volunteered
                : null);

        var result = await CreateService(auth)
            .UpdateAsync(_tenantId, _accommodationId, stay.Id, UpdateRequest(_member), TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        auth.Verify(a => a.ClassifyIntentActorAsync(_tenantId, _householdId, _member, It.IsAny<CancellationToken>()), Times.Once);
        auth.Verify(a => a.ClassifyIntentActorAsync(_tenantId, Guid.Empty, _member, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_HouseholdManagerCannotReassignStayEvenWithinManagedHousehold()
    {
        var stay = await SeedStayAsync(_member);
        var auth = CreateAuthorization(
            canAssign: false,
            classify: (householdId, memberId) => householdId == _householdId && memberId == _member
                ? IntentSource.Volunteered
                : householdId == _householdId2 && memberId == _member2
                    ? IntentSource.Volunteered
                    : null);

        var result = await CreateService(auth)
            .UpdateAsync(_tenantId, _accommodationId, stay.Id, UpdateRequest(_member2), TestContext.Current.CancellationToken);

        Assert.False(result.Successful);
        var persisted = await _dbContext.AccommodationIntents.AsNoTracking()
            .SingleAsync(i => i.Id == stay.Id, TestContext.Current.CancellationToken);
        Assert.Equal(_member, persisted.HouseholdMemberId);
        auth.Verify(a => a.ClassifyIntentActorAsync(_tenantId, _householdId, _member, It.IsAny<CancellationToken>()), Times.Once);
        auth.Verify(a => a.ClassifyIntentActorAsync(_tenantId, _householdId2, _member2, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_UndefinedStatusReturnsErrorWithoutChanges()
    {
        var stay = await SeedStayAsync(_member);

        var result = await CreateService()
            .UpdateAsync(_tenantId, _accommodationId, stay.Id, UpdateRequest(_member, (AccommodationIntentStatus)999), TestContext.Current.CancellationToken);

        Assert.False(result.Successful);
        var persisted = await _dbContext.AccommodationIntents.AsNoTracking()
            .SingleAsync(i => i.Id == stay.Id, TestContext.Current.CancellationToken);
        Assert.Equal(AccommodationIntentStatus.Requested, persisted.Status);
        Assert.Equal("Original notes", persisted.Notes);
    }

    [Fact]
    public async Task UpdateAsync_NonCoordinatorCannotEditExistingManagedStatus()
    {
        var stay = await SeedStayAsync(_member, AccommodationIntentStatus.Hold);

        var result = await CreateService()
            .UpdateAsync(_tenantId, _accommodationId, stay.Id, UpdateRequest(_member), TestContext.Current.CancellationToken);

        Assert.False(result.Successful);
        var persisted = await _dbContext.AccommodationIntents.AsNoTracking()
            .SingleAsync(i => i.Id == stay.Id, TestContext.Current.CancellationToken);
        Assert.Equal(AccommodationIntentStatus.Hold, persisted.Status);
        Assert.Equal("Original notes", persisted.Notes);
        Assert.Equal(2, persisted.PartyAdults);
    }

    [Fact]
    public async Task UpdateAsync_CoordinatorCanReassignAcrossHouseholds()
    {
        var stay = await SeedStayAsync(_member);
        var auth = CreateAuthorization(canManageEvent: true, classify: (householdId, memberId) =>
            householdId == _householdId && memberId == _member
                || householdId == _householdId2 && memberId == _member2
                ? IntentSource.Assigned
                : null);

        var result = await CreateService(auth)
            .UpdateAsync(_tenantId, _accommodationId, stay.Id,
                UpdateRequest(_member2, AccommodationIntentStatus.Confirmed, _accommodationId2),
                TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        Assert.Equal(_member2, result.Entity!.HouseholdMemberId);
        Assert.Equal(_accommodationId2, result.Entity.AccommodationId);
        Assert.Equal(AccommodationIntentStatus.Confirmed, result.Entity.Status);
        auth.Verify(a => a.ClassifyIntentActorAsync(_tenantId, _householdId, _member, It.IsAny<CancellationToken>()), Times.Once);
        auth.Verify(a => a.ClassifyIntentActorAsync(_tenantId, _householdId2, _member2, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_TargetMemberNotFoundReturnsErrorWithoutChanges()
    {
        var outsideMemberId = Guid.NewGuid();
        var stay = await SeedStayAsync(_member);

        var result = await CreateService(canManageEvent: true)
            .UpdateAsync(_tenantId, _accommodationId, stay.Id, UpdateRequest(outsideMemberId), TestContext.Current.CancellationToken);

        Assert.False(result.Successful);
        Assert.Contains(result.Messages, m => m.Type == MessageType.ERROR && m.Message.Contains("member", StringComparison.OrdinalIgnoreCase));
        var persisted = await _dbContext.AccommodationIntents.AsNoTracking()
            .SingleAsync(i => i.Id == stay.Id, TestContext.Current.CancellationToken);
        Assert.Equal(_member, persisted.HouseholdMemberId);
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
        var persisted = await _dbContext.AccommodationIntents.AsNoTracking()
            .SingleAsync(i => i.Id == created.Entity!.Id, TestContext.Current.CancellationToken);
        Assert.Equal(_accommodationId, persisted.AccommodationId);
        Assert.Equal(Day1, persisted.StartNight);
        Assert.Null(persisted.Notes);
    }

    [Fact]
    public async Task UpdateAsync_InvertedSpan_ReturnsErrorWithoutChanges()
    {
        var stay = await SeedStayAsync(_member);

        var result = await CreateService()
            .UpdateAsync(_tenantId, _accommodationId, stay.Id,
                UpdateRequest(_member, startNight: Day3, endNight: Day1),
                TestContext.Current.CancellationToken);

        Assert.False(result.Successful);
        var persisted = await _dbContext.AccommodationIntents.AsNoTracking()
            .SingleAsync(i => i.Id == stay.Id, TestContext.Current.CancellationToken);
        Assert.Equal(Day1, persisted.StartNight);
        Assert.Equal(Day2, persisted.EndNight);
        Assert.Equal("Original notes", persisted.Notes);
        Assert.Equal(2, persisted.PartyAdults);
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
