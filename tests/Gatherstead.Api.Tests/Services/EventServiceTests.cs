using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.Events;
using Gatherstead.Api.Services.Planning;
using Gatherstead.Api.Tests.Fixtures;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Moq;

namespace Gatherstead.Api.Tests.Services;

/// <summary>Covers the tenant event read (<see cref="EventService.ListAsync"/>).</summary>
public class EventServiceTests : IAsyncLifetime
{
    private GathersteadDbContext _dbContext = null!;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _propertyId = Guid.NewGuid();

    private static readonly DateOnly Jun1 = new(2025, 6, 1);

    public async ValueTask InitializeAsync()
    {
        _dbContext = TestDbContextFactory.Create(tenantId: _tenantId);
        _dbContext.Tenants.Add(new Tenant { Id = _tenantId, Name = "Test Tenant" });
        _dbContext.Properties.Add(new Property { Id = _propertyId, TenantId = _tenantId, Name = "Lake House" });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        _dbContext.Dispose();
        return ValueTask.CompletedTask;
    }

    private EventService CreateService() =>
        new(_dbContext,
            Mock.Of<ICurrentTenantContext>(c => c.TenantId == _tenantId),
            Mock.Of<IMemberAuthorizationService>(),
            new PlanSyncService(_dbContext),
            Mock.Of<IAuditVisibilityContext>());

    private async Task AddEventAsync(string name)
    {
        _dbContext.Events.Add(new Event
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, PropertyId = _propertyId,
            Name = name, StartDate = Jun1, EndDate = Jun1,
        });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ListAsync_ReturnsEventsForTenant()
    {
        // Guard: this List already materializes before mapping; keep it exercised under SQLite so it
        // cannot regress into an untranslatable instance-method projection.
        await AddEventAsync("Summer Reunion");
        await AddEventAsync("Winter Retreat");

        var result = await CreateService().ListAsync(_tenantId, null, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        Assert.Equal(2, result.Entity!.Count);
    }
}
