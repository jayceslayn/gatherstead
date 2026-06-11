using Gatherstead.Api.Security;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.Reports;
using Gatherstead.Api.Tests.Fixtures;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Gatherstead.Api.Tests.Services;

public class EventReportServiceTests : IAsyncLifetime
{
    private GathersteadDbContext _dbContext = null!;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _propertyId = Guid.NewGuid();
    private readonly Guid _eventId = Guid.NewGuid();
    private readonly Guid _householdId = Guid.NewGuid();
    private readonly Guid _templateId = Guid.NewGuid();

    private static readonly DateOnly Day1 = new(2025, 6, 1);
    private static readonly DateOnly Day2 = new(2025, 6, 2);

    private readonly Guid _alice = Guid.NewGuid();
    private readonly Guid _bob = Guid.NewGuid();
    private readonly Guid _carol = Guid.NewGuid();

    public async ValueTask InitializeAsync()
    {
        _dbContext = TestDbContextFactory.Create(tenantId: _tenantId);
        _dbContext.Tenants.Add(new Tenant { Id = _tenantId, Name = "Test Tenant" });
        _dbContext.Properties.Add(new Property { Id = _propertyId, TenantId = _tenantId, Name = "Lake House" });
        _dbContext.Events.Add(new Event
        {
            Id = _eventId,
            TenantId = _tenantId,
            PropertyId = _propertyId,
            Name = "Summer Reunion",
            StartDate = Day1,
            EndDate = Day2,
        });
        _dbContext.Households.Add(new Household { Id = _householdId, TenantId = _tenantId, Name = "Smith Household" });

        _dbContext.HouseholdMembers.AddRange(
            new HouseholdMember { Id = _alice, TenantId = _tenantId, HouseholdId = _householdId, Name = "Alice", DietaryTags = ["vegan"] },
            new HouseholdMember { Id = _bob, TenantId = _tenantId, HouseholdId = _householdId, Name = "Bob", DietaryTags = ["Vegan"] },
            new HouseholdMember { Id = _carol, TenantId = _tenantId, HouseholdId = _householdId, Name = "Carol", DietaryTags = ["kosher"] });

        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        _dbContext.Dispose();
        return ValueTask.CompletedTask;
    }

    private EventReportService CreateService(bool canReadSensitive = true)
    {
        var tenantContext = Mock.Of<ICurrentTenantContext>(c => c.TenantId == _tenantId);
        var scope = canReadSensitive ? SensitiveReadScope.Global : SensitiveReadScope.None;
        var auth = Mock.Of<IMemberAuthorizationService>(a =>
            a.GetSensitiveReadScopeAsync(_tenantId, It.IsAny<CancellationToken>()) == Task.FromResult(scope));
        return new EventReportService(_dbContext, tenantContext, auth);
    }

    private async Task SeedMealPlanWithAttendanceAsync()
    {
        _dbContext.MealTemplates.Add(new MealTemplate
        {
            Id = _templateId,
            TenantId = _tenantId,
            EventId = _eventId,
            Name = "Day 1 Dinner",
            MealTypes = MealTypeFlags.Dinner,
        });

        var planId = Guid.NewGuid();
        _dbContext.MealPlans.Add(new MealPlan
        {
            Id = planId,
            TenantId = _tenantId,
            MealTemplateId = _templateId,
            Day = Day1,
            MealType = MealType.Dinner,
        });

        _dbContext.MealAttendances.AddRange(
            new MealAttendance { Id = Guid.NewGuid(), TenantId = _tenantId, MealPlanId = planId, HouseholdMemberId = _alice, Status = AttendanceStatus.Going },
            new MealAttendance { Id = Guid.NewGuid(), TenantId = _tenantId, MealPlanId = planId, HouseholdMemberId = _bob, Status = AttendanceStatus.Going, BringOwnFood = true },
            new MealAttendance { Id = Guid.NewGuid(), TenantId = _tenantId, MealPlanId = planId, HouseholdMemberId = _carol, Status = AttendanceStatus.Maybe });

        _dbContext.EventAttendances.AddRange(
            new EventAttendance { Id = Guid.NewGuid(), TenantId = _tenantId, EventId = _eventId, HouseholdMemberId = _alice, Day = Day1, Status = AttendanceStatus.Going },
            new EventAttendance { Id = Guid.NewGuid(), TenantId = _tenantId, EventId = _eventId, HouseholdMemberId = _bob, Day = Day1, Status = AttendanceStatus.Going },
            new EventAttendance { Id = Guid.NewGuid(), TenantId = _tenantId, EventId = _eventId, HouseholdMemberId = _carol, Day = Day1, Status = AttendanceStatus.Maybe });

        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetEventMealReportAsync_WithoutSensitiveRead_ReturnsError()
    {
        var result = await CreateService(canReadSensitive: false)
            .GetEventMealReportAsync(_tenantId, _eventId, TestContext.Current.CancellationToken);

        Assert.False(result.Successful);
    }

    [Fact]
    public async Task GetEventMealReportAsync_UnknownEvent_ReturnsError()
    {
        var result = await CreateService()
            .GetEventMealReportAsync(_tenantId, Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.False(result.Successful);
    }

    [Fact]
    public async Task GetEventMealReportAsync_EmitsOneDayPerEventDate()
    {
        var result = await CreateService().GetEventMealReportAsync(_tenantId, _eventId, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        Assert.Equal(2, result.Entity!.Days.Count);
        Assert.Equal(Day1, result.Entity.Days[0].Day);
        Assert.Equal(Day2, result.Entity.Days[1].Day);
    }

    [Fact]
    public async Task GetEventMealReportAsync_AggregatesPerMealCounts()
    {
        await SeedMealPlanWithAttendanceAsync();

        var result = await CreateService().GetEventMealReportAsync(_tenantId, _eventId, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        var day1 = result.Entity!.Days.Single(d => d.Day == Day1);
        Assert.Equal(2, day1.Going);
        Assert.Equal(1, day1.Maybe);

        var meal = Assert.Single(day1.Meals);
        Assert.Equal(MealType.Dinner, meal.MealType);
        Assert.Equal("Day 1 Dinner", meal.TemplateName);
        Assert.Equal(2, meal.Going);
        Assert.Equal(1, meal.Maybe);
        Assert.Equal(0, meal.NotGoing);
        Assert.Equal(1, meal.BringOwnFood);

        // NotGoing attendees are excluded; Going + Maybe are included.
        Assert.Equal(3, meal.Attendees.Count);
    }

    [Fact]
    public async Task GetEventMealReportAsync_DietaryTallyGroupsByMemberCombo()
    {
        await SeedMealPlanWithAttendanceAsync();

        var result = await CreateService().GetEventMealReportAsync(_tenantId, _eventId, TestContext.Current.CancellationToken);

        var meal = result.Entity!.Days.Single(d => d.Day == Day1).Meals.Single();
        // "vegan" and "Vegan" slugs both resolve to display name "Vegan" (case-insensitive lookup
        // in the DietaryTag seed table) → same combo → count 2.
        var veganCombo = Assert.Single(meal.Dietary, d => string.Equals(d.Label, "Vegan", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(2, veganCombo.Count);
        // Carol (Maybe) has "kosher" tag → resolves to "Kosher" → her own combo group.
        var kosherCombo = Assert.Single(meal.Dietary, d => string.Equals(d.Label, "Kosher", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, kosherCombo.Count);
    }

    [Fact]
    public async Task GetEventMealReportAsync_NoData_ReturnsDaysWithoutMeals()
    {
        var result = await CreateService().GetEventMealReportAsync(_tenantId, _eventId, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        Assert.All(result.Entity!.Days, d => Assert.Empty(d.Meals));
        Assert.All(result.Entity.Days, d => Assert.Equal(0, d.Going));
        Assert.All(result.Entity.Days, d => Assert.Empty(d.Tasks));
        Assert.All(result.Entity.Days, d => Assert.Empty(d.Accommodations));
    }

    // Creates a task plan on Day1 with the given minimum assignees, and assigns the supplied
    // members to it. Returns the plan id so callers can flag it as an exception, etc.
    private async Task<Guid> SeedTaskPlanAsync(int? minimumAssignees, params Guid[] assignees)
    {
        var templateId = Guid.NewGuid();
        _dbContext.TaskTemplates.Add(new TaskTemplate
        {
            Id = templateId,
            TenantId = _tenantId,
            EventId = _eventId,
            Name = "Set Up",
            TimeSlots = TaskTimeSlotFlags.Morning,
            MinimumAssignees = minimumAssignees,
        });

        var planId = Guid.NewGuid();
        _dbContext.TaskPlans.Add(new TaskPlan
        {
            Id = planId,
            TenantId = _tenantId,
            TemplateId = templateId,
            Day = Day1,
            TimeSlot = TaskTimeSlot.Morning,
        });

        foreach (var memberId in assignees)
        {
            _dbContext.TaskIntents.Add(new TaskIntent
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                TaskPlanId = planId,
                HouseholdMemberId = memberId,
                Volunteered = true,
            });
        }

        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        return planId;
    }

    [Fact]
    public async Task GetEventMealReportAsync_TaskCoverage_ReportsAssigneeCountAndNames()
    {
        // min 2, two assignees → "covered" by the client-side threshold.
        await SeedTaskPlanAsync(minimumAssignees: 2, _alice, _bob);

        var result = await CreateService().GetEventMealReportAsync(_tenantId, _eventId, TestContext.Current.CancellationToken);

        var task = Assert.Single(result.Entity!.Days.Single(d => d.Day == Day1).Tasks);
        Assert.Equal("Set Up", task.TemplateName);
        Assert.Equal(TaskTimeSlot.Morning, task.TimeSlot);
        Assert.Equal(2, task.MinimumAssignees);
        Assert.Equal(2, task.AssigneeCount);
        Assert.Equal(["Alice", "Bob"], task.Assignees);
        Assert.False(task.IsException);
    }

    [Fact]
    public async Task GetEventMealReportAsync_TaskCoverage_PartialAndOpenReflectAssigneeCount()
    {
        // min 2, one assignee → "partial".
        await SeedTaskPlanAsync(minimumAssignees: 2, _alice);

        var task = (await CreateService().GetEventMealReportAsync(_tenantId, _eventId, TestContext.Current.CancellationToken))
            .Entity!.Days.Single(d => d.Day == Day1).Tasks.Single();

        Assert.Equal(2, task.MinimumAssignees);
        Assert.Equal(1, task.AssigneeCount);
        Assert.Equal(["Alice"], task.Assignees);
    }

    [Fact]
    public async Task GetEventMealReportAsync_TaskCoverage_OpenWhenNoIntents()
    {
        await SeedTaskPlanAsync(minimumAssignees: 1);

        var task = (await CreateService().GetEventMealReportAsync(_tenantId, _eventId, TestContext.Current.CancellationToken))
            .Entity!.Days.Single(d => d.Day == Day1).Tasks.Single();

        Assert.Equal(0, task.AssigneeCount);
        Assert.Empty(task.Assignees);
    }

    [Fact]
    public async Task GetEventMealReportAsync_TaskException_IsFlagged()
    {
        var planId = await SeedTaskPlanAsync(minimumAssignees: 1, _alice);
        var plan = await _dbContext.TaskPlans.SingleAsync(p => p.Id == planId, TestContext.Current.CancellationToken);
        plan.IsException = true;
        plan.ExceptionReason = "Cancelled — venue closed";
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var task = (await CreateService().GetEventMealReportAsync(_tenantId, _eventId, TestContext.Current.CancellationToken))
            .Entity!.Days.Single(d => d.Day == Day1).Tasks.Single();

        Assert.True(task.IsException);
        Assert.Equal("Cancelled — venue closed", task.ExceptionReason);
    }

    private Guid SeedAccommodation()
    {
        var accommodationId = Guid.NewGuid();
        _dbContext.Accommodations.Add(new Accommodation
        {
            Id = accommodationId,
            TenantId = _tenantId,
            PropertyId = _propertyId,
            Name = "Cabin A",
            Type = AccommodationType.Bedroom,
            CapacityAdults = 4,
            CapacityChildren = 2,
            Notes = "Lake views. No capes near the fireplace.",
        });
        return accommodationId;
    }

    [Fact]
    public async Task GetEventMealReportAsync_AccommodationOccupancy_SumsPartySizeExcludingDeclined()
    {
        var accommodationId = SeedAccommodation();
        _dbContext.AccommodationIntents.AddRange(
            // PartySize 5 — counts fully.
            new AccommodationIntent { Id = Guid.NewGuid(), TenantId = _tenantId, AccommodationId = accommodationId, HouseholdMemberId = _alice, Night = Day1, Status = AccommodationIntentStatus.Confirmed, Decision = AccommodationIntentDecision.Approved, PartySize = 5 },
            // Null PartySize — counts as 1.
            new AccommodationIntent { Id = Guid.NewGuid(), TenantId = _tenantId, AccommodationId = accommodationId, HouseholdMemberId = _bob, Night = Day1, Status = AccommodationIntentStatus.Hold, Decision = AccommodationIntentDecision.Pending, PartySize = null },
            // Declined — excluded entirely.
            new AccommodationIntent { Id = Guid.NewGuid(), TenantId = _tenantId, AccommodationId = accommodationId, HouseholdMemberId = _carol, Night = Day1, Status = AccommodationIntentStatus.Intent, Decision = AccommodationIntentDecision.Declined, PartySize = 3 });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await CreateService().GetEventMealReportAsync(_tenantId, _eventId, TestContext.Current.CancellationToken);

        var accommodation = Assert.Single(result.Entity!.Days.Single(d => d.Day == Day1).Accommodations);
        Assert.Equal("Cabin A", accommodation.Name);
        Assert.Equal(4, accommodation.CapacityAdults);
        Assert.Equal(2, accommodation.CapacityChildren);
        Assert.Equal("Lake views. No capes near the fireplace.", accommodation.Notes);
        Assert.Equal(6, accommodation.Occupied); // 5 + 1 (declined excluded)
        Assert.Equal(2, accommodation.Occupants.Count);
        Assert.Equal(["Alice", "Bob"], accommodation.Occupants.Select(o => o.Name));
    }

    [Fact]
    public async Task GetEventMealReportAsync_Accommodation_OmittedOnNightsWithoutOccupants()
    {
        var accommodationId = SeedAccommodation();
        // Only Day1 has an intent; Day2 should not list the accommodation at all.
        _dbContext.AccommodationIntents.Add(new AccommodationIntent
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            AccommodationId = accommodationId,
            HouseholdMemberId = _alice,
            Night = Day1,
            Status = AccommodationIntentStatus.Confirmed,
            Decision = AccommodationIntentDecision.Approved,
            PartySize = 2,
        });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await CreateService().GetEventMealReportAsync(_tenantId, _eventId, TestContext.Current.CancellationToken);

        Assert.Single(result.Entity!.Days.Single(d => d.Day == Day1).Accommodations);
        Assert.Empty(result.Entity.Days.Single(d => d.Day == Day2).Accommodations);
    }
}
