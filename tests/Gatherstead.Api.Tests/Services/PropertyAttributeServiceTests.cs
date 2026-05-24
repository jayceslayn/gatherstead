using Gatherstead.Api.Contracts.PropertyAttributes;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.PropertyAttributes;
using Gatherstead.Api.Tests.Services.Attributes;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Moq;

namespace Gatherstead.Api.Tests.Services;

public class PropertyAttributeServiceTests
    : ParentScopedAttributeServiceTestBase<
        PropertyAttribute, PropertyAttributeDto,
        CreatePropertyAttributeRequest, UpdatePropertyAttributeRequest,
        IPropertyAttributeService>
{
    protected override void SeedParent(GathersteadDbContext db)
        => db.Properties.Add(new Property { Id = ParentId, TenantId = TenantId, Name = "Test Property" });

    protected override PropertyAttribute NewAttribute(string key, string value, byte tenantMinRole) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = TenantId,
        PropertyId = ParentId,
        Key = key,
        Value = value,
        TenantMinRole = tenantMinRole,
    };

    protected override CreatePropertyAttributeRequest NewCreateRequest(string key, string value, byte tenantMinRole)
        => new() { Key = key, Value = value, TenantMinRole = tenantMinRole };

    protected override UpdatePropertyAttributeRequest NewUpdateRequest(string key, string value, byte tenantMinRole)
        => new() { Key = key, Value = value, TenantMinRole = tenantMinRole };

    protected override IPropertyAttributeService MakeService(TenantRole? callerTenantRole, bool canManage)
    {
        var tenantContext = Mock.Of<ICurrentTenantContext>(c => c.TenantId == TenantId);
        return BuildService(tenantContext, callerTenantRole, canManage);
    }

    protected override IPropertyAttributeService MakeServiceWithContext(ICurrentTenantContext tenantContext, bool canManage)
        => BuildService(tenantContext, TenantRole.Manager, canManage);

    private IPropertyAttributeService BuildService(ICurrentTenantContext tenantContext, TenantRole? callerRole, bool canManage)
    {
        var auth = new Mock<IMemberAuthorizationService>();
        auth.Setup(a => a.GetCallerTenantRoleAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(callerRole);
        auth.Setup(a => a.CanManageTenantAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(canManage);
        return new PropertyAttributeService(DbContext, tenantContext, auth.Object);
    }
}
