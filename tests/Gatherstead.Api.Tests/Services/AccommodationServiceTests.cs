using Gatherstead.Api.Contracts.Accommodations;
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

    private AccommodationService CreateManagerService()
    {
        var auth = Mock.Of<IMemberAuthorizationService>(a =>
            a.CanManageTenantAsync(_tenantId, It.IsAny<CancellationToken>()) == Task.FromResult(true));
        return new AccommodationService(
            _dbContext,
            Mock.Of<ICurrentTenantContext>(c => c.TenantId == _tenantId),
            auth,
            Mock.Of<IAuditVisibilityContext>());
    }

    private void AddAccommodation(string name) =>
        _dbContext.Accommodations.Add(new Accommodation
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, PropertyId = _propertyId,
            Name = name, Type = AccommodationType.Bedroom,
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

    [Fact]
    public async Task CreateAsync_PersistsBedsAndDimensions_GetReturnsThem()
    {
        var service = CreateManagerService();
        var created = await service.CreateAsync(_tenantId, _propertyId, new CreateAccommodationRequest
        {
            Name = "Cabin A",
            Type = AccommodationType.Bedroom,
            WidthMeters = 3m,
            DepthMeters = 4m,
            Beds = [new BedWriteEntry(BedSize.Queen, 2), new BedWriteEntry(BedSize.Single, 1)],
        }, TestContext.Current.CancellationToken);

        Assert.True(created.Successful);

        var got = await service.GetAsync(_tenantId, _propertyId, created.Entity!.Id, TestContext.Current.CancellationToken);
        Assert.True(got.Successful);
        Assert.Equal(3m, got.Entity!.WidthMeters);
        Assert.Equal(4m, got.Entity.DepthMeters);
        Assert.Null(got.Entity.AreaSqMeters);
        Assert.Equal(12m, got.Entity.EffectiveAreaSqMeters); // 3 × 4, no override
        Assert.Equal(2, got.Entity.Beds.Count);
        Assert.Equal(2, got.Entity.Beds.Single(b => b.Size == BedSize.Queen).Quantity);
        Assert.Equal(1, got.Entity.Beds.Single(b => b.Size == BedSize.Single).Quantity);
    }

    [Fact]
    public async Task CreateAsync_AreaOverride_WinsOverWidthTimesDepth()
    {
        var service = CreateManagerService();
        var created = await service.CreateAsync(_tenantId, _propertyId, new CreateAccommodationRequest
        {
            Name = "Irregular Room",
            Type = AccommodationType.Bedroom,
            WidthMeters = 3m,
            DepthMeters = 4m,
            AreaSqMeters = 20m,
        }, TestContext.Current.CancellationToken);

        var got = await service.GetAsync(_tenantId, _propertyId, created.Entity!.Id, TestContext.Current.CancellationToken);
        Assert.Equal(20m, got.Entity!.EffectiveAreaSqMeters);
    }

    [Fact]
    public async Task UpdateAsync_Beds_FullReplace()
    {
        var service = CreateManagerService();
        var created = await service.CreateAsync(_tenantId, _propertyId, new CreateAccommodationRequest
        {
            Name = "Cabin A",
            Type = AccommodationType.Bedroom,
            Beds = [new BedWriteEntry(BedSize.Queen, 2)],
        }, TestContext.Current.CancellationToken);

        await service.UpdateAsync(_tenantId, _propertyId, created.Entity!.Id, new UpdateAccommodationRequest
        {
            Name = "Cabin A",
            Type = AccommodationType.Bedroom,
            Beds = [new BedWriteEntry(BedSize.Single, 3)],
        }, TestContext.Current.CancellationToken);

        var got = await service.GetAsync(_tenantId, _propertyId, created.Entity.Id, TestContext.Current.CancellationToken);
        var bed = Assert.Single(got.Entity!.Beds);
        Assert.Equal(BedSize.Single, bed.Size);
        Assert.Equal(3, bed.Quantity);
    }

    [Fact]
    public async Task CreateAsync_DuplicateName_ReturnsConflictCodeWithParams()
    {
        AddAccommodation("Lakeside Cabin");
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await CreateManagerService().CreateAsync(_tenantId, _propertyId, new CreateAccommodationRequest
        {
            Name = "Lakeside Cabin",
            Type = AccommodationType.Bedroom,
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Successful);
        var error = Assert.Single(result.Messages, m => m.Type == MessageType.ERROR);
        Assert.Equal(ErrorCode.ENTITY_CONFLICT, error.Code);
        Assert.NotNull(error.Params);
        Assert.Equal("accommodation", error.Params!["entity"]);
        Assert.Equal("Lakeside Cabin", error.Params!["name"]);
    }

    [Fact]
    public async Task UpdateAsync_RenameToExistingName_ReturnsConflictCodeWithParams()
    {
        var service = CreateManagerService();
        AddAccommodation("Barn Loft");
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var toRename = await service.CreateAsync(_tenantId, _propertyId, new CreateAccommodationRequest
        {
            Name = "Guest Room",
            Type = AccommodationType.Bedroom,
        }, TestContext.Current.CancellationToken);

        var result = await service.UpdateAsync(_tenantId, _propertyId, toRename.Entity!.Id, new UpdateAccommodationRequest
        {
            Name = "Barn Loft",
            Type = AccommodationType.Bedroom,
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Successful);
        var error = Assert.Single(result.Messages, m => m.Type == MessageType.ERROR);
        Assert.Equal(ErrorCode.ENTITY_CONFLICT, error.Code);
        Assert.Equal("Barn Loft", error.Params!["name"]);
    }
}
