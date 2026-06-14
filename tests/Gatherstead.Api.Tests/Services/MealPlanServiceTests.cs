using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.MealPlans;
using Gatherstead.Api.Services.Planning;
using Gatherstead.Api.Tests.Fixtures;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Gatherstead.Api.Tests.Services;

public class MealPlanServiceTests : IAsyncLifetime
{
    private GathersteadDbContext _dbContext = null!;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _propertyId = Guid.NewGuid();
    private readonly Guid _eventId = Guid.NewGuid();
    private readonly Guid _templateId = Guid.NewGuid();

    private static readonly DateOnly Jun1 = new(2025, 6, 1);
    private static readonly DateOnly Jun2 = new(2025, 6, 2);

    public async ValueTask InitializeAsync()
    {
        _dbContext = TestDbContextFactory.Create(tenantId: _tenantId);
        _dbContext.Tenants.Add(new Tenant { Id = _tenantId, Name = "Test Tenant" });
        _dbContext.Properties.Add(new Property { Id = _propertyId, TenantId = _tenantId, Name = "Lake House" });
        _dbContext.Events.Add(new Event
        {
            Id = _eventId, TenantId = _tenantId, PropertyId = _propertyId,
            Name = "Summer Reunion", StartDate = Jun1, EndDate = Jun2,
        });
        _dbContext.MealTemplates.Add(new MealTemplate
        {
            Id = _templateId, TenantId = _tenantId, EventId = _eventId,
            Name = "Meals", MealTypes = MealTypeFlags.Breakfast,
        });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        _dbContext.Dispose();
        return ValueTask.CompletedTask;
    }

    private MealPlanService CreateService()
    {
        var tenantContext = Mock.Of<ICurrentTenantContext>(c => c.TenantId == _tenantId);
        var auth = Mock.Of<IMemberAuthorizationService>(a =>
            a.CanManageEventAsync(_tenantId, It.IsAny<CancellationToken>()) == Task.FromResult(true)
            && a.GetCallerTenantRoleAsync(_tenantId, It.IsAny<CancellationToken>()) == Task.FromResult((TenantRole?)TenantRole.Owner));
        return new MealPlanService(_dbContext, tenantContext, auth, Mock.Of<IAuditVisibilityContext>());
    }

    private async Task<MealPlan> AddPlanAsync()
    {
        var plan = new MealPlan
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, MealTemplateId = _templateId,
            Day = Jun1, MealType = MealType.Breakfast,
        };
        _dbContext.MealPlans.Add(plan);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        return plan;
    }

    [Fact]
    public async Task DeleteAsync_SetsSuppressionMarker()
    {
        var plan = await AddPlanAsync();

        var result = await CreateService().DeleteAsync(_tenantId, _templateId, plan.Id, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        var stored = await _dbContext.MealPlans.IgnoreQueryFilters()
            .SingleAsync(p => p.Id == plan.Id, TestContext.Current.CancellationToken);
        Assert.True(stored.IsDeleted);
        Assert.True(stored.IsException);
    }

    [Fact]
    public async Task DeleteAsync_ExcludesPlanFromDefaultQuery()
    {
        var plan = await AddPlanAsync();

        await CreateService().DeleteAsync(_tenantId, _templateId, plan.Id, TestContext.Current.CancellationToken);

        // The soft-delete global query filter hides the suppressed plan from normal reads.
        var visible = await _dbContext.MealPlans
            .Where(p => p.MealTemplateId == _templateId)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Empty(visible);
    }

    [Fact]
    public async Task DeleteAsync_SuppressedPlanSurvivesRegeneration()
    {
        var plan = await AddPlanAsync();
        await CreateService().DeleteAsync(_tenantId, _templateId, plan.Id, TestContext.Current.CancellationToken);

        var template = await _dbContext.MealTemplates.FindAsync([_templateId], TestContext.Current.CancellationToken);
        await new PlanSyncService(_dbContext).SyncMealPlanAsync(_tenantId, template!, Jun1, Jun2, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // The suppressed Jun1 breakfast must not be resurrected; only Jun2 breakfast is generated.
        var active = await _dbContext.MealPlans.ToListAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain(active, p => p.Day == Jun1);
        Assert.Contains(active, p => p.Day == Jun2 && p.MealType == MealType.Breakfast);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsNotFound_WhenPlanMissing()
    {
        var result = await CreateService().DeleteAsync(_tenantId, _templateId, Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.Null(result.Entity);
    }
}
