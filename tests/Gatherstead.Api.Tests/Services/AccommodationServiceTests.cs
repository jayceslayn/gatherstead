using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Services.Accommodations;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Tests.Fixtures;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Moq;

namespace Gatherstead.Api.Tests.Services;

/// <summary>Covers the per-property accommodation read (<see cref="AccommodationService.ListAsync"/>).</summary>
public class AccommodationServiceTests : IAsyncLifetime
{
    private GathersteadDbContext _dbContext = null!;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _propertyId = Guid.NewGuid();

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

    private AccommodationService CreateService() =>
        new(_dbContext,
            Mock.Of<ICurrentTenantContext>(c => c.TenantId == _tenantId),
            Mock.Of<IMemberAuthorizationService>(),
            Mock.Of<IAuditVisibilityContext>());

    private void AddAccommodation(string name) =>
        _dbContext.Accommodations.Add(new Accommodation
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, PropertyId = _propertyId,
            Name = name, Type = AccommodationType.Bedroom, CapacityAdults = 4,
        });

    [Fact]
    public async Task ListAsync_ReturnsAccommodationsForProperty()
    {
        // Guard: this List already materializes before mapping; keep it exercised under SQLite so it
        // cannot regress into an untranslatable instance-method projection.
        AddAccommodation("Cabin A");
        AddAccommodation("Cabin B");
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await CreateService().ListAsync(_tenantId, _propertyId, null, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        Assert.Equal(2, result.Entity!.Count);
    }
}
