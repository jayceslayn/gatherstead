using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.Properties;
using Gatherstead.Api.Tests.Fixtures;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Moq;

namespace Gatherstead.Api.Tests.Services;

/// <summary>Covers the tenant property read (<see cref="PropertyService.ListAsync"/>).</summary>
public class PropertyServiceTests : IAsyncLifetime
{
    private GathersteadDbContext _dbContext = null!;
    private readonly Guid _tenantId = Guid.NewGuid();

    public async ValueTask InitializeAsync()
    {
        _dbContext = TestDbContextFactory.Create(tenantId: _tenantId);
        _dbContext.Tenants.Add(new Tenant { Id = _tenantId, Name = "Test Tenant" });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        _dbContext.Dispose();
        return ValueTask.CompletedTask;
    }

    private PropertyService CreateService() =>
        new(_dbContext,
            Mock.Of<ICurrentTenantContext>(c => c.TenantId == _tenantId),
            Mock.Of<IMemberAuthorizationService>(),
            Mock.Of<IAuditVisibilityContext>());

    [Fact]
    public async Task ListAsync_ReturnsPropertiesForTenant()
    {
        // Guard: this List already materializes before mapping; keep it exercised under SQLite so it
        // cannot regress into an untranslatable instance-method projection.
        _dbContext.Properties.Add(new Property { Id = Guid.NewGuid(), TenantId = _tenantId, Name = "Lake House" });
        _dbContext.Properties.Add(new Property { Id = Guid.NewGuid(), TenantId = _tenantId, Name = "Cabin" });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await CreateService().ListAsync(_tenantId, null, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        Assert.Equal(2, result.Entity!.Count);
    }
}
