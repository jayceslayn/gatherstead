using Gatherstead.Api.Contracts.HouseholdAttributes;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.HouseholdAttributes;
using Gatherstead.Api.Tests.Services.Attributes;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Moq;

namespace Gatherstead.Api.Tests.Services;

public class HouseholdAttributeServiceTests
    : ParentScopedAttributeServiceTestBase<
        HouseholdAttribute, HouseholdAttributeDto,
        CreateHouseholdAttributeRequest, UpdateHouseholdAttributeRequest,
        IHouseholdAttributeService>
{
    protected override void SeedParent(GathersteadDbContext db)
        => db.Households.Add(new Household { Id = ParentId, TenantId = TenantId, Name = "Test Household" });

    protected override HouseholdAttribute NewAttribute(string key, string value, byte tenantMinRole) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = TenantId,
        HouseholdId = ParentId,
        Key = key,
        Value = value,
        TenantMinRole = tenantMinRole,
    };

    protected override CreateHouseholdAttributeRequest NewCreateRequest(string key, string value, byte tenantMinRole)
        => new() { Key = key, Value = value, TenantMinRole = tenantMinRole };

    protected override UpdateHouseholdAttributeRequest NewUpdateRequest(string key, string value, byte tenantMinRole)
        => new() { Key = key, Value = value, TenantMinRole = tenantMinRole };

    protected override IHouseholdAttributeService MakeService(TenantRole? callerTenantRole, bool canManage)
    {
        var tenantContext = Mock.Of<ICurrentTenantContext>(c => c.TenantId == TenantId);
        return BuildService(tenantContext, callerTenantRole, callerHouseholdRole: null, canManageHousehold: canManage);
    }

    protected override IHouseholdAttributeService MakeServiceWithContext(ICurrentTenantContext tenantContext, bool canManage)
        => BuildService(tenantContext, TenantRole.Manager, callerHouseholdRole: null, canManageHousehold: canManage);

    private IHouseholdAttributeService BuildService(
        ICurrentTenantContext tenantContext,
        TenantRole? callerTenantRole,
        HouseholdRole? callerHouseholdRole,
        bool canManageHousehold)
    {
        var auth = new Mock<IMemberAuthorizationService>();
        auth.Setup(a => a.GetCallerTenantRoleAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(callerTenantRole);
        auth.Setup(a => a.GetCallerHouseholdRoleAsync(TenantId, ParentId, It.IsAny<CancellationToken>())).ReturnsAsync(callerHouseholdRole);
        auth.Setup(a => a.CanManageHouseholdAsync(TenantId, ParentId, It.IsAny<CancellationToken>())).ReturnsAsync(canManageHousehold);
        return new HouseholdAttributeService(DbContext, tenantContext, auth.Object);
    }

    // ── Additional household-bypass scenarios (beyond the shared base tests) ──

    private async Task<HouseholdAttribute> SeedHouseholdAttrAsync(string key, byte tenantMinRole, byte? householdMinRole)
    {
        var attr = NewAttribute(key, "v", tenantMinRole);
        attr.HouseholdMinRole = householdMinRole;
        DbContext.HouseholdAttributes.Add(attr);
        await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        return attr;
    }

    [Fact]
    public async Task ListAsync_HouseholdBypass_VisibleToHouseholdMemberInSameHousehold()
    {
        await SeedHouseholdAttrAsync("Secret", (byte)TenantRole.Manager, (byte)HouseholdRole.Member);
        var tenantContext = Mock.Of<ICurrentTenantContext>(c => c.TenantId == TenantId);
        var service = BuildService(tenantContext, TenantRole.Member, HouseholdRole.Member, canManageHousehold: false);
        var result = await service.ListAsync(TenantId, ParentId, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(result.Successful);
        Assert.Single(result.Entity!);
    }

    [Fact]
    public async Task ListAsync_HouseholdBypass_HiddenFromMemberInDifferentHousehold()
    {
        await SeedHouseholdAttrAsync("Secret", (byte)TenantRole.Manager, (byte)HouseholdRole.Member);
        var tenantContext = Mock.Of<ICurrentTenantContext>(c => c.TenantId == TenantId);
        var service = BuildService(tenantContext, TenantRole.Member, callerHouseholdRole: null, canManageHousehold: false);
        var result = await service.ListAsync(TenantId, ParentId, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(result.Successful);
        Assert.Empty(result.Entity!);
    }

    [Fact]
    public async Task ListAsync_HouseholdBypass_HouseholdMemberRoleBlocksManagerOnlyAttr()
    {
        await SeedHouseholdAttrAsync("Secret", (byte)TenantRole.Manager, (byte)HouseholdRole.Manager);
        var tenantContext = Mock.Of<ICurrentTenantContext>(c => c.TenantId == TenantId);
        var service = BuildService(tenantContext, TenantRole.Member, HouseholdRole.Member, canManageHousehold: false);
        var result = await service.ListAsync(TenantId, ParentId, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(result.Successful);
        Assert.Empty(result.Entity!);
    }

    [Fact]
    public async Task ListAsync_HouseholdBypass_HouseholdManagerBypassesManagerTenantRole()
    {
        await SeedHouseholdAttrAsync("Secret", (byte)TenantRole.Manager, (byte)HouseholdRole.Manager);
        var tenantContext = Mock.Of<ICurrentTenantContext>(c => c.TenantId == TenantId);
        var service = BuildService(tenantContext, TenantRole.Member, HouseholdRole.Manager, canManageHousehold: false);
        var result = await service.ListAsync(TenantId, ParentId, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(result.Successful);
        Assert.Single(result.Entity!);
    }

    [Fact]
    public async Task CreateAsync_StoresHouseholdMinRole()
    {
        var request = new CreateHouseholdAttributeRequest
        {
            Key = "PrivateNote",
            Value = "...",
            TenantMinRole = (byte)TenantRole.Manager,
            HouseholdMinRole = (byte)HouseholdRole.Member,
        };
        var tenantContext = Mock.Of<ICurrentTenantContext>(c => c.TenantId == TenantId);
        var service = BuildService(tenantContext, TenantRole.Manager, callerHouseholdRole: null, canManageHousehold: true);
        var result = await service.CreateAsync(TenantId, ParentId, request, TestContext.Current.CancellationToken);
        Assert.True(result.Successful);
        Assert.Equal((byte)HouseholdRole.Member, result.Entity!.HouseholdMinRole);
    }
}
