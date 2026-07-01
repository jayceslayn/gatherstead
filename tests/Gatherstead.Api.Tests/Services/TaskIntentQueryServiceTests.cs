using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.TaskIntents;
using Gatherstead.Api.Tests.Fixtures;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Moq;

namespace Gatherstead.Api.Tests.Services;

/// <summary>Covers the cross-event "my tasks" read (<see cref="TaskIntentService.ListForMemberAsync"/>).</summary>
public class TaskIntentQueryServiceTests : IAsyncLifetime
{
    private GathersteadDbContext _dbContext = null!;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _propertyId = Guid.NewGuid();
    private readonly Guid _eventId = Guid.NewGuid();
    private readonly Guid _templateId = Guid.NewGuid();
    private readonly Guid _pastPlanId = Guid.NewGuid();
    private readonly Guid _futurePlanId = Guid.NewGuid();
    private readonly Guid _householdId = Guid.NewGuid();
    private readonly Guid _member = Guid.NewGuid();
    private readonly Guid _member2 = Guid.NewGuid();

    private static readonly DateOnly Past = new(2025, 6, 1);
    private static readonly DateOnly Future = new(2025, 6, 10);
    private static readonly DateOnly Cutoff = new(2025, 6, 5);

    public async ValueTask InitializeAsync()
    {
        _dbContext = TestDbContextFactory.Create(tenantId: _tenantId);
        _dbContext.Tenants.Add(new Tenant { Id = _tenantId, Name = "Test Tenant" });
        _dbContext.Properties.Add(new Property { Id = _propertyId, TenantId = _tenantId, Name = "Lake House" });
        _dbContext.Events.Add(new Event
        {
            Id = _eventId, TenantId = _tenantId, PropertyId = _propertyId,
            Name = "Summer Reunion", StartDate = Past, EndDate = Future,
        });
        _dbContext.TaskTemplates.Add(new TaskTemplate
        {
            Id = _templateId, TenantId = _tenantId, EventId = _eventId, Name = "Firewood",
        });
        _dbContext.TaskPlans.Add(new TaskPlan { Id = _pastPlanId, TenantId = _tenantId, TemplateId = _templateId, Day = Past });
        _dbContext.TaskPlans.Add(new TaskPlan { Id = _futurePlanId, TenantId = _tenantId, TemplateId = _templateId, Day = Future });
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

    private TaskIntentService CreateService()
    {
        var tenantContext = Mock.Of<ICurrentTenantContext>(c => c.TenantId == _tenantId);
        return new TaskIntentService(_dbContext, tenantContext, Mock.Of<IMemberAuthorizationService>(), Mock.Of<IAuditVisibilityContext>());
    }

    private async Task AddIntentAsync(Guid planId, Guid memberId, bool volunteered)
    {
        _dbContext.TaskIntents.Add(new TaskIntent
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, TaskPlanId = planId,
            HouseholdMemberId = memberId, Volunteered = volunteered,
        });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ListForMemberAsync_EnrichesWithTaskAndEventContext()
    {
        await AddIntentAsync(_futurePlanId, _member, volunteered: true);

        var result = await CreateService().ListForMemberAsync(_tenantId, new[] { _member }, null, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        var task = Assert.Single(result.Entity!);
        Assert.Equal("Firewood", task.TaskName);
        Assert.Equal("Summer Reunion", task.EventName);
        Assert.Equal(_eventId, task.EventId);
        Assert.Equal(Future, task.Day);
    }

    [Fact]
    public async Task ListForMemberAsync_ExcludesNonVolunteeredIntents()
    {
        await AddIntentAsync(_futurePlanId, _member, volunteered: false);

        var result = await CreateService().ListForMemberAsync(_tenantId, new[] { _member }, null, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        Assert.Empty(result.Entity!);
    }

    [Fact]
    public async Task ListForMemberAsync_MemberFilter_ExcludesOtherMembers()
    {
        await AddIntentAsync(_futurePlanId, _member2, volunteered: true);

        var result = await CreateService().ListForMemberAsync(_tenantId, new[] { _member }, null, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        Assert.Empty(result.Entity!);
    }

    [Fact]
    public async Task ListForMemberAsync_FromDay_ExcludesPastPlans()
    {
        await AddIntentAsync(_pastPlanId, _member, volunteered: true);
        await AddIntentAsync(_futurePlanId, _member, volunteered: true);

        var result = await CreateService().ListForMemberAsync(_tenantId, new[] { _member }, Cutoff, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        var task = Assert.Single(result.Entity!);
        Assert.Equal(Future, task.Day);
    }

    [Fact]
    public async Task ListAsync_ReturnsIntentsForPlan()
    {
        // Regression: the per-plan list projection must materialize before mapping, otherwise EF Core
        // rejects the instance MapToDto in the query shaper and the endpoint 500s.
        await AddIntentAsync(_futurePlanId, _member, volunteered: true);

        var result = await CreateService().ListAsync(_tenantId, _futurePlanId, null, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        var intent = Assert.Single(result.Entity!);
        Assert.Equal(_member, intent.HouseholdMemberId);
    }

    [Fact]
    public async Task ListAsync_MemberFilter_ExcludesOtherMembers()
    {
        await AddIntentAsync(_futurePlanId, _member2, volunteered: true);

        var result = await CreateService().ListAsync(_tenantId, _futurePlanId, new[] { _member }, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        Assert.Empty(result.Entity!);
    }
}
