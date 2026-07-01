using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.Planning;
using Gatherstead.Api.Services.TaskTemplates;
using Gatherstead.Api.Tests.Fixtures;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Moq;

namespace Gatherstead.Api.Tests.Services;

/// <summary>Covers the per-event template read (<see cref="TaskTemplateService.ListAsync"/>).</summary>
public class TaskTemplateServiceTests : IAsyncLifetime
{
    private GathersteadDbContext _dbContext = null!;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _propertyId = Guid.NewGuid();
    private readonly Guid _eventId = Guid.NewGuid();

    private static readonly DateOnly Jun1 = new(2025, 6, 1);

    public async ValueTask InitializeAsync()
    {
        _dbContext = TestDbContextFactory.Create(tenantId: _tenantId);
        _dbContext.Tenants.Add(new Tenant { Id = _tenantId, Name = "Test Tenant" });
        _dbContext.Properties.Add(new Property { Id = _propertyId, TenantId = _tenantId, Name = "Lake House" });
        _dbContext.Events.Add(new Event
        {
            Id = _eventId, TenantId = _tenantId, PropertyId = _propertyId,
            Name = "Summer Reunion", StartDate = Jun1, EndDate = Jun1,
        });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        _dbContext.Dispose();
        return ValueTask.CompletedTask;
    }

    private TaskTemplateService CreateService() =>
        new(_dbContext,
            Mock.Of<ICurrentTenantContext>(c => c.TenantId == _tenantId),
            Mock.Of<IMemberAuthorizationService>(),
            new PlanSyncService(_dbContext),
            Mock.Of<IAuditVisibilityContext>());

    [Fact]
    public async Task ListAsync_ReturnsTemplatesForEvent()
    {
        // Guard: this List already materializes before mapping; keep it exercised under SQLite so it
        // cannot regress into an untranslatable instance-method projection.
        _dbContext.TaskTemplates.Add(new TaskTemplate { Id = Guid.NewGuid(), TenantId = _tenantId, EventId = _eventId, Name = "Firewood" });
        _dbContext.TaskTemplates.Add(new TaskTemplate { Id = Guid.NewGuid(), TenantId = _tenantId, EventId = _eventId, Name = "Cleanup" });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await CreateService().ListAsync(_tenantId, _eventId, null, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        Assert.Equal(2, result.Entity!.Count);
    }
}
