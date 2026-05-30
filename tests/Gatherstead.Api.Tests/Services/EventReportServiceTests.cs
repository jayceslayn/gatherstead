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
            new HouseholdMember { Id = _carol, TenantId = _tenantId, HouseholdId = _householdId, Name = "Carol" });

        _dbContext.DietaryProfiles.Add(new DietaryProfile
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            HouseholdMemberId = _carol,
            PreferredDiet = "Kosher",
            Allergies = ["peanuts"],
            Restrictions = [],
        });

        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        _dbContext.Dispose();
        return ValueTask.CompletedTask;
    }

    private EventReportService CreateService(bool canManageEvent = true)
    {
        var tenantContext = Mock.Of<ICurrentTenantContext>(c => c.TenantId == _tenantId);
        var auth = Mock.Of<IMemberAuthorizationService>(a =>
            a.CanManageEventAsync(_tenantId, It.IsAny<CancellationToken>()) == Task.FromResult(canManageEvent));
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
    public async Task GetEventMealReportAsync_WithoutEventManage_ReturnsError()
    {
        var result = await CreateService(canManageEvent: false)
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
    public async Task GetEventMealReportAsync_DietaryTallyIsCaseInsensitive()
    {
        await SeedMealPlanWithAttendanceAsync();

        var result = await CreateService().GetEventMealReportAsync(_tenantId, _eventId, TestContext.Current.CancellationToken);

        var meal = result.Entity!.Days.Single(d => d.Day == Day1).Meals.Single();
        // "vegan" + "Vegan" collapse to one tally with count 2.
        var vegan = Assert.Single(meal.Dietary, d => string.Equals(d.Label, "vegan", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(2, vegan.Count);
        // Carol (Maybe) contributes her profile-derived labels too.
        Assert.Contains(meal.Dietary, d => d.Label == "Kosher");
        Assert.Contains(meal.Dietary, d => d.Label == "peanuts");
    }

    [Fact]
    public async Task GetEventMealReportAsync_NoData_ReturnsDaysWithoutMeals()
    {
        var result = await CreateService().GetEventMealReportAsync(_tenantId, _eventId, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        Assert.All(result.Entity!.Days, d => Assert.Empty(d.Meals));
        Assert.All(result.Entity.Days, d => Assert.Equal(0, d.Going));
    }
}
