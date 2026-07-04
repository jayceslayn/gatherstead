using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Contracts.TaskIntents;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.TaskIntents;
using Gatherstead.Api.Tests.Fixtures;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Moq;

namespace Gatherstead.Api.Tests.Services;

/// <summary>Covers the server-derived <see cref="IntentSource"/> semantics on task-intent upsert.</summary>
public class TaskIntentSourceTests : IAsyncLifetime
{
    private GathersteadDbContext _dbContext = null!;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _propertyId = Guid.NewGuid();
    private readonly Guid _eventId = Guid.NewGuid();
    private readonly Guid _templateId = Guid.NewGuid();
    private readonly Guid _planId = Guid.NewGuid();
    private readonly Guid _householdId = Guid.NewGuid();
    private readonly Guid _member = Guid.NewGuid();

    public async ValueTask InitializeAsync()
    {
        _dbContext = TestDbContextFactory.Create(tenantId: _tenantId);
        _dbContext.Tenants.Add(new Tenant { Id = _tenantId, Name = "Test Tenant" });
        _dbContext.Properties.Add(new Property { Id = _propertyId, TenantId = _tenantId, Name = "Lake House" });
        _dbContext.Events.Add(new Event { Id = _eventId, TenantId = _tenantId, PropertyId = _propertyId, Name = "Reunion", StartDate = new(2025, 6, 1), EndDate = new(2025, 6, 3) });
        _dbContext.TaskTemplates.Add(new TaskTemplate { Id = _templateId, TenantId = _tenantId, EventId = _eventId, Name = "Firewood" });
        _dbContext.TaskPlans.Add(new TaskPlan { Id = _planId, TenantId = _tenantId, TemplateId = _templateId, Day = new(2025, 6, 2) });
        _dbContext.Households.Add(new Household { Id = _householdId, TenantId = _tenantId, Name = "Smith" });
        _dbContext.HouseholdMembers.Add(new HouseholdMember { Id = _member, TenantId = _tenantId, HouseholdId = _householdId, Name = "Alice" });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        _dbContext.Dispose();
        return ValueTask.CompletedTask;
    }

    private TaskIntentService CreateService(IntentSource classifiedAs)
    {
        var tenantContext = Mock.Of<ICurrentTenantContext>(c => c.TenantId == _tenantId);
        var auth = Mock.Of<IMemberAuthorizationService>(a =>
            a.ClassifyIntentActorAsync(_tenantId, It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())
                == Task.FromResult<IntentSource?>(classifiedAs) &&
            a.CanAssignIntentForMemberAsync(_tenantId, It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())
                == Task.FromResult(true));
        return new TaskIntentService(_dbContext, tenantContext, auth, Mock.Of<IAuditVisibilityContext>());
    }

    private UpsertTaskIntentRequest Request() => new() { HouseholdMemberId = _member };

    [Fact]
    public async Task UpsertAsync_Create_SetsSourceFromClassification()
    {
        var result = await CreateService(IntentSource.Assigned)
            .UpsertAsync(_tenantId, _planId, _householdId, Request(), TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        Assert.Equal(IntentSource.Assigned, result.Entity!.Source);
    }

    [Fact]
    public async Task UpsertAsync_ReUpsertLiveRow_PreservesOriginalSource()
    {
        // First sign-up is Volunteered; a later coordinator re-assert must not flip it to Assigned.
        await CreateService(IntentSource.Volunteered)
            .UpsertAsync(_tenantId, _planId, _householdId, Request(), TestContext.Current.CancellationToken);

        var result = await CreateService(IntentSource.Assigned)
            .UpsertAsync(_tenantId, _planId, _householdId, Request(), TestContext.Current.CancellationToken);

        Assert.Equal(IntentSource.Volunteered, result.Entity!.Source);
    }

    [Fact]
    public async Task UpsertAsync_ReviveSoftDeletedRow_ResetsSource()
    {
        var created = await CreateService(IntentSource.Volunteered)
            .UpsertAsync(_tenantId, _planId, _householdId, Request(), TestContext.Current.CancellationToken);

        // Withdraw (soft-delete) the row.
        await CreateService(IntentSource.Volunteered)
            .DeleteAsync(_tenantId, _planId, created.Entity!.Id, TestContext.Current.CancellationToken);

        // Re-sign-up, this time classified as Assigned → Source is reset on revive.
        var revived = await CreateService(IntentSource.Assigned)
            .UpsertAsync(_tenantId, _planId, _householdId, Request(), TestContext.Current.CancellationToken);

        Assert.True(revived.Successful);
        Assert.Equal(IntentSource.Assigned, revived.Entity!.Source);
    }
}
