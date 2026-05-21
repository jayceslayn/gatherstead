using Gatherstead.Api.Services.Planning;
using Gatherstead.Api.Tests.Fixtures;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Api.Tests.Services;

public class PlanSyncServiceTests : IAsyncLifetime
{
    private GathersteadDbContext _dbContext = null!;
    private PlanSyncService _planSyncService = null!;

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _propertyId = Guid.NewGuid();
    private readonly Guid _eventId = Guid.NewGuid();

    private static readonly DateOnly Jan1 = new(2025, 1, 1);
    private static readonly DateOnly Jan2 = new(2025, 1, 2);
    private static readonly DateOnly Jan5 = new(2025, 1, 5);

    public async ValueTask InitializeAsync()
    {
        _dbContext = TestDbContextFactory.Create(tenantId: _tenantId);
        _planSyncService = new PlanSyncService(_dbContext);

        _dbContext.Tenants.Add(new Tenant { Id = _tenantId, Name = "Test Tenant" });
        _dbContext.Properties.Add(new Property { Id = _propertyId, TenantId = _tenantId, Name = "Test Property" });
        _dbContext.Events.Add(new Event
        {
            Id = _eventId,
            TenantId = _tenantId,
            PropertyId = _propertyId,
            Name = "Test Event",
            StartDate = Jan1,
            EndDate = Jan2,
        });
        await _dbContext.SaveChangesAsync();
    }

    public ValueTask DisposeAsync()
    {
        _dbContext.Dispose();
        return ValueTask.CompletedTask;
    }

    // ── SyncMealPlanAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task SyncMealPlanAsync_CreatesOnePlanPerDayPerMealType()
    {
        var template = new MealTemplate
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, EventId = _eventId,
            Name = "Meals", MealTypes = MealTypeFlags.Breakfast | MealTypeFlags.Dinner,
        };
        _dbContext.MealTemplates.Add(template);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _planSyncService.SyncMealPlanAsync(_tenantId, template, Jan1, Jan2, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var plans = await _dbContext.MealPlans.ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(4, plans.Count); // 2 days × 2 types
        Assert.Contains(plans, p => p.Day == Jan1 && p.MealType == MealType.Breakfast);
        Assert.Contains(plans, p => p.Day == Jan1 && p.MealType == MealType.Dinner);
        Assert.Contains(plans, p => p.Day == Jan2 && p.MealType == MealType.Breakfast);
        Assert.Contains(plans, p => p.Day == Jan2 && p.MealType == MealType.Dinner);
    }

    [Fact]
    public async Task SyncMealPlanAsync_RestoresSoftDeletedNonExceptionPlan()
    {
        var template = new MealTemplate
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, EventId = _eventId,
            Name = "Meals", MealTypes = MealTypeFlags.Breakfast,
        };
        _dbContext.MealTemplates.Add(template);
        var existingPlan = new MealPlan
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, MealTemplateId = template.Id,
            Day = Jan1, MealType = MealType.Breakfast, IsDeleted = true, IsException = false,
        };
        _dbContext.MealPlans.Add(existingPlan);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _planSyncService.SyncMealPlanAsync(_tenantId, template, Jan1, Jan1, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.False(existingPlan.IsDeleted);
    }

    [Fact]
    public async Task SyncMealPlanAsync_PrunesPlanOutsideDateRange()
    {
        var template = new MealTemplate
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, EventId = _eventId,
            Name = "Meals", MealTypes = MealTypeFlags.Breakfast,
        };
        _dbContext.MealTemplates.Add(template);
        var outOfRangePlan = new MealPlan
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, MealTemplateId = template.Id,
            Day = Jan5, MealType = MealType.Breakfast,
        };
        _dbContext.MealPlans.Add(outOfRangePlan);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _planSyncService.SyncMealPlanAsync(_tenantId, template, Jan1, Jan2, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.True(outOfRangePlan.IsDeleted);
        // New plans for Jan1 and Jan2 should be created
        var activePlans = await _dbContext.MealPlans.ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, activePlans.Count);
    }

    [Fact]
    public async Task SyncMealPlanAsync_DoesNotRestoreExceptionMarker()
    {
        var template = new MealTemplate
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, EventId = _eventId,
            Name = "Meals", MealTypes = MealTypeFlags.Breakfast,
        };
        _dbContext.MealTemplates.Add(template);
        var exceptionPlan = new MealPlan
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, MealTemplateId = template.Id,
            Day = Jan1, MealType = MealType.Breakfast, IsDeleted = true, IsException = true,
        };
        _dbContext.MealPlans.Add(exceptionPlan);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _planSyncService.SyncMealPlanAsync(_tenantId, template, Jan1, Jan1, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.True(exceptionPlan.IsDeleted); // suppression marker left intact
        var activePlans = await _dbContext.MealPlans.ToListAsync(TestContext.Current.CancellationToken);
        Assert.Empty(activePlans); // no new plan created either
    }

    // ── SyncTaskPlanAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task SyncTaskPlanAsync_CreatesOnePlanPerDayPerTimeSlot()
    {
        var template = new TaskTemplate
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, EventId = _eventId,
            Name = "Tasks", TimeSlots = TaskTimeSlotFlags.Morning | TaskTimeSlotFlags.Evening,
        };
        _dbContext.TaskTemplates.Add(template);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _planSyncService.SyncTaskPlanAsync(_tenantId, template, Jan1, Jan2, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var plans = await _dbContext.TaskPlans.ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(4, plans.Count); // 2 days × 2 slots
        Assert.Contains(plans, p => p.Day == Jan1 && p.TimeSlot == TaskTimeSlot.Morning);
        Assert.Contains(plans, p => p.Day == Jan1 && p.TimeSlot == TaskTimeSlot.Evening);
        Assert.Contains(plans, p => p.Day == Jan2 && p.TimeSlot == TaskTimeSlot.Morning);
        Assert.Contains(plans, p => p.Day == Jan2 && p.TimeSlot == TaskTimeSlot.Evening);
    }

    [Fact]
    public async Task SyncTaskPlanAsync_DoesNotRestoreExceptionMarker()
    {
        var template = new TaskTemplate
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, EventId = _eventId,
            Name = "Tasks", TimeSlots = TaskTimeSlotFlags.Morning,
        };
        _dbContext.TaskTemplates.Add(template);
        var exceptionPlan = new TaskPlan
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, TemplateId = template.Id,
            Day = Jan1, TimeSlot = TaskTimeSlot.Morning, IsDeleted = true, IsException = true,
        };
        _dbContext.TaskPlans.Add(exceptionPlan);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _planSyncService.SyncTaskPlanAsync(_tenantId, template, Jan1, Jan1, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.True(exceptionPlan.IsDeleted);
        var activePlans = await _dbContext.TaskPlans.ToListAsync(TestContext.Current.CancellationToken);
        Assert.Empty(activePlans);
    }

    // ── SyncEventPlansAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task SyncEventPlansAsync_ProcessesAllMealAndTaskTemplates()
    {
        var mealTemplate = new MealTemplate
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, EventId = _eventId,
            Name = "Meals", MealTypes = MealTypeFlags.Lunch,
        };
        var taskTemplate = new TaskTemplate
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, EventId = _eventId,
            Name = "Tasks", TimeSlots = TaskTimeSlotFlags.Anytime,
        };
        _dbContext.MealTemplates.Add(mealTemplate);
        _dbContext.TaskTemplates.Add(taskTemplate);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var @event = await _dbContext.Events.FindAsync([_eventId], TestContext.Current.CancellationToken);
        await _planSyncService.SyncEventPlansAsync(_tenantId, @event!, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var mealPlans = await _dbContext.MealPlans.ToListAsync(TestContext.Current.CancellationToken);
        var taskPlans = await _dbContext.TaskPlans.ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, mealPlans.Count);  // Jan1 + Jan2, Lunch
        Assert.Equal(2, taskPlans.Count); // Jan1 + Jan2, Anytime
    }
}
