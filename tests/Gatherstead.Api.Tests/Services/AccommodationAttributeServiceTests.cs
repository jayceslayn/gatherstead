using Gatherstead.Api.Contracts.AccommodationAttributes;
using Gatherstead.Api.Services.AccommodationAttributes;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Tests.Services.Attributes;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Moq;

namespace Gatherstead.Api.Tests.Services;

public class AccommodationAttributeServiceTests
    : ParentScopedAttributeServiceTestBase<
        AccommodationAttribute, AccommodationAttributeDto,
        CreateAccommodationAttributeRequest, UpdateAccommodationAttributeRequest,
        IAccommodationAttributeService>
{
    private readonly Guid _propertyId = Guid.NewGuid();

    protected override void SeedParent(GathersteadDbContext db)
    {
        db.Properties.Add(new Property { Id = _propertyId, TenantId = TenantId, Name = "Test Property" });
        db.Accommodations.Add(new Accommodation { Id = ParentId, TenantId = TenantId, PropertyId = _propertyId, Name = "Test Accommodation" });
    }

    protected override AccommodationAttribute NewAttribute(string key, string value, byte tenantMinRole) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = TenantId,
        AccommodationId = ParentId,
        Key = key,
        Value = value,
        TenantMinRole = tenantMinRole,
    };

    protected override CreateAccommodationAttributeRequest NewCreateRequest(string key, string value, byte tenantMinRole)
        => new() { Key = key, Value = value, TenantMinRole = tenantMinRole };

    protected override UpdateAccommodationAttributeRequest NewUpdateRequest(string key, string value, byte tenantMinRole)
        => new() { Key = key, Value = value, TenantMinRole = tenantMinRole };

    protected override IAccommodationAttributeService MakeService(TenantRole? callerTenantRole, bool canManage)
    {
        var tenantContext = Mock.Of<ICurrentTenantContext>(c => c.TenantId == TenantId);
        return BuildService(tenantContext, callerTenantRole, canManage);
    }

    protected override IAccommodationAttributeService MakeServiceWithContext(ICurrentTenantContext tenantContext, bool canManage)
        => BuildService(tenantContext, TenantRole.Manager, canManage);

    private IAccommodationAttributeService BuildService(ICurrentTenantContext tenantContext, TenantRole? callerRole, bool canManage)
    {
        var auth = new Mock<IMemberAuthorizationService>();
        auth.Setup(a => a.GetCallerTenantRoleAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(callerRole);
        auth.Setup(a => a.CanManageTenantAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(canManage);
        return new AccommodationAttributeService(DbContext, tenantContext, auth.Object);
    }
}
