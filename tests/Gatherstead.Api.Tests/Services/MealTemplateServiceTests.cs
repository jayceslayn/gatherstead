using Gatherstead.Api.Contracts.MealTemplates;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.MealTemplates;
using Gatherstead.Api.Services.Planning;
using Gatherstead.Api.Tests.Fixtures;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Gatherstead.Api.Tests.Services;

public class MealTemplateServiceTests : IAsyncLifetime
{
    private GathersteadDbContext _dbContext = null!;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _propertyId = Guid.NewGuid();
    private readonly Guid _eventId = Guid.NewGuid();

    private static readonly DateOnly Jun1 = new(2025, 6, 1);
    private static readonly DateOnly Jun3 = new(2025, 6, 3);

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
            StartDate = Jun1,
            EndDate = Jun3,
        });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        _dbContext.Dispose();
        return ValueTask.CompletedTask;
    }

    private MealTemplateService CreateService()
    {
        var tenantContext = Mock.Of<ICurrentTenantContext>(c => c.TenantId == _tenantId);
        var auth = Mock.Of<IMemberAuthorizationService>(a =>
            a.CanManageEventAsync(_tenantId, It.IsAny<CancellationToken>()) == Task.FromResult(true)
            && a.GetCallerTenantRoleAsync(_tenantId, It.IsAny<CancellationToken>()) == Task.FromResult((TenantRole?)TenantRole.Owner));
        return new MealTemplateService(_dbContext, tenantContext, auth, new PlanSyncService(_dbContext),
            Mock.Of<Gatherstead.Api.Contracts.Responses.IAuditVisibilityContext>());
    }

    [Fact]
    public async Task CreateAsync_WithoutMatchingTask_DoesNotCreateTaskTemplate()
    {
        var request = new CreateMealTemplateRequest
        {
            Name = "Saturday Breakfast",
            MealTypes = MealTypeFlags.Breakfast,
            CreateMatchingTaskTemplate = false,
        };

        var result = await CreateService().CreateAsync(_tenantId, _eventId, request, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        var taskTemplates = await _dbContext.TaskTemplates.ToListAsync(TestContext.Current.CancellationToken);
        Assert.Empty(taskTemplates);
    }

    [Fact]
    public async Task CreateAsync_WithMatchingTask_CreatesTaskTemplateWithMappedSlotsAndAlignedPlans()
    {
        var request = new CreateMealTemplateRequest
        {
            Name = "Main Meals",
            MealTypes = MealTypeFlags.Breakfast | MealTypeFlags.Dinner,
            CreateMatchingTaskTemplate = true,
        };

        var result = await CreateService().CreateAsync(_tenantId, _eventId, request, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);

        var taskTemplate = await _dbContext.TaskTemplates.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Main Meals", taskTemplate.Name);
        // Breakfast→Morning, Dinner→Evening
        Assert.Equal(TaskTimeSlotFlags.Morning | TaskTimeSlotFlags.Evening, taskTemplate.TimeSlots);

        // Task plans align day-for-day with the event range (3 days × 2 slots).
        var taskPlans = await _dbContext.TaskPlans
            .Where(p => p.TemplateId == taskTemplate.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(6, taskPlans.Count);
        Assert.Contains(taskPlans, p => p.Day == Jun1 && p.TimeSlot == TaskTimeSlot.Morning);
        Assert.Contains(taskPlans, p => p.Day == Jun3 && p.TimeSlot == TaskTimeSlot.Evening);
    }

    [Fact]
    public async Task CreateAsync_WithMatchingTask_AppendsSuffixOnNameConflict()
    {
        _dbContext.TaskTemplates.Add(new TaskTemplate
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            EventId = _eventId,
            Name = "Lunch",
            TimeSlots = TaskTimeSlotFlags.Anytime,
        });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var request = new CreateMealTemplateRequest
        {
            Name = "Lunch",
            MealTypes = MealTypeFlags.Lunch,
            CreateMatchingTaskTemplate = true,
        };

        var result = await CreateService().CreateAsync(_tenantId, _eventId, request, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        var names = await _dbContext.TaskTemplates.Select(t => t.Name).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Lunch", names);
        Assert.Contains("Lunch (cook)", names);
    }
}
