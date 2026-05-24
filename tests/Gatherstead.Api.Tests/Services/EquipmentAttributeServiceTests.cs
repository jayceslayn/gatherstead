using Gatherstead.Api.Contracts.EquipmentAttributes;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.EquipmentAttributes;
using Gatherstead.Api.Tests.Services.Attributes;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Moq;

namespace Gatherstead.Api.Tests.Services;

public class EquipmentAttributeServiceTests
    : ParentScopedAttributeServiceTestBase<
        EquipmentAttribute, EquipmentAttributeDto,
        CreateEquipmentAttributeRequest, UpdateEquipmentAttributeRequest,
        IEquipmentAttributeService>
{
    protected override void SeedParent(GathersteadDbContext db)
        => db.Equipment.Add(new Equipment { Id = ParentId, TenantId = TenantId, Name = "Test Equipment" });

    protected override EquipmentAttribute NewAttribute(string key, string value, byte tenantMinRole) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = TenantId,
        EquipmentId = ParentId,
        Key = key,
        Value = value,
        TenantMinRole = tenantMinRole,
    };

    protected override CreateEquipmentAttributeRequest NewCreateRequest(string key, string value, byte tenantMinRole)
        => new() { Key = key, Value = value, TenantMinRole = tenantMinRole };

    protected override UpdateEquipmentAttributeRequest NewUpdateRequest(string key, string value, byte tenantMinRole)
        => new() { Key = key, Value = value, TenantMinRole = tenantMinRole };

    protected override IEquipmentAttributeService MakeService(TenantRole? callerTenantRole, bool canManage)
    {
        var tenantContext = Mock.Of<ICurrentTenantContext>(c => c.TenantId == TenantId);
        return BuildService(tenantContext, callerTenantRole, canManage);
    }

    protected override IEquipmentAttributeService MakeServiceWithContext(ICurrentTenantContext tenantContext, bool canManage)
        => BuildService(tenantContext, TenantRole.Manager, canManage);

    private IEquipmentAttributeService BuildService(ICurrentTenantContext tenantContext, TenantRole? callerRole, bool canManage)
    {
        var auth = new Mock<IMemberAuthorizationService>();
        auth.Setup(a => a.GetCallerTenantRoleAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(callerRole);
        auth.Setup(a => a.CanManageTenantAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(canManage);
        return new EquipmentAttributeService(DbContext, tenantContext, auth.Object);
    }
}
