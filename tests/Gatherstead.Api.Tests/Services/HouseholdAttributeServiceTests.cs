using Gatherstead.Api.Contracts.HouseholdAttributes;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.HouseholdAttributes;
using Gatherstead.Api.Tests.Fixtures;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Moq;

namespace Gatherstead.Api.Tests.Services;

public class HouseholdAttributeServiceTests : IAsyncLifetime
{
    private GathersteadDbContext _dbContext = null!;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _householdId = Guid.NewGuid();

    public async ValueTask InitializeAsync()
    {
        _dbContext = TestDbContextFactory.Create(tenantId: _tenantId);
        _dbContext.Tenants.Add(new Tenant { Id = _tenantId, Name = "Test Tenant" });
        _dbContext.Households.Add(new Household { Id = _householdId, TenantId = _tenantId, Name = "Test Household" });
        await _dbContext.SaveChangesAsync();
    }

    public ValueTask DisposeAsync()
    {
        _dbContext.Dispose();
        return ValueTask.CompletedTask;
    }

    private HouseholdAttributeService CreateService(
        TenantRole? callerTenantRole,
        HouseholdRole? callerHouseholdRole = null,
        bool canManageHousehold = false)
    {
        var tenantContext = Mock.Of<ICurrentTenantContext>(c => c.TenantId == _tenantId);
        var auth = new Mock<IMemberAuthorizationService>();
        auth.Setup(a => a.GetCallerTenantRoleAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(callerTenantRole);
        auth.Setup(a => a.GetCallerHouseholdRoleAsync(_tenantId, _householdId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(callerHouseholdRole);
        auth.Setup(a => a.CanManageHouseholdAsync(_tenantId, _householdId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(canManageHousehold);
        return new HouseholdAttributeService(_dbContext, tenantContext, auth.Object);
    }

    private async Task<HouseholdAttribute> SeedAttributeAsync(
        string key = "TestKey",
        string value = "TestValue",
        byte tenantMinRole = (byte)TenantRole.Member,
        byte? householdMinRole = null)
    {
        var attr = new HouseholdAttribute
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            HouseholdId = _householdId,
            Key = key,
            Value = value,
            TenantMinRole = tenantMinRole,
            HouseholdMinRole = householdMinRole,
        };
        _dbContext.HouseholdAttributes.Add(attr);
        await _dbContext.SaveChangesAsync();
        return attr;
    }

    // ── Tenant-role visibility ───────────────────────────────────────────────

    [Fact]
    public async Task ListAsync_MemberAttribute_VisibleToMember()
    {
        await SeedAttributeAsync(tenantMinRole: (byte)TenantRole.Member);
        var result = await CreateService(TenantRole.Member)
            .ListAsync(_tenantId, _householdId, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(result.Successful);
        Assert.Single(result.Entity!);
    }

    [Fact]
    public async Task ListAsync_ManagerAttribute_HiddenFromMember()
    {
        await SeedAttributeAsync(tenantMinRole: (byte)TenantRole.Manager, householdMinRole: null);
        var result = await CreateService(TenantRole.Member, callerHouseholdRole: null)
            .ListAsync(_tenantId, _householdId, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(result.Successful);
        Assert.Empty(result.Entity!);
    }

    [Fact]
    public async Task ListAsync_ManagerAttribute_VisibleToManager()
    {
        await SeedAttributeAsync(tenantMinRole: (byte)TenantRole.Manager);
        var result = await CreateService(TenantRole.Manager)
            .ListAsync(_tenantId, _householdId, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(result.Successful);
        Assert.Single(result.Entity!);
    }

    // ── Household-role bypass ────────────────────────────────────────────────

    [Fact]
    public async Task ListAsync_HouseholdBypass_VisibleToHouseholdMemberInSameHousehold()
    {
        // TenantMinRole=Manager would block a plain Member, but HouseholdMinRole=Member grants bypass.
        await SeedAttributeAsync(tenantMinRole: (byte)TenantRole.Manager, householdMinRole: (byte)HouseholdRole.Member);
        var result = await CreateService(TenantRole.Member, callerHouseholdRole: HouseholdRole.Member)
            .ListAsync(_tenantId, _householdId, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(result.Successful);
        Assert.Single(result.Entity!);
    }

    [Fact]
    public async Task ListAsync_HouseholdBypass_HiddenFromMemberInDifferentHousehold()
    {
        // Caller is a household Member elsewhere; GetCallerHouseholdRoleAsync for THIS household returns null.
        await SeedAttributeAsync(tenantMinRole: (byte)TenantRole.Manager, householdMinRole: (byte)HouseholdRole.Member);
        var result = await CreateService(TenantRole.Member, callerHouseholdRole: null)
            .ListAsync(_tenantId, _householdId, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(result.Successful);
        Assert.Empty(result.Entity!);
    }

    [Fact]
    public async Task ListAsync_HouseholdBypass_HouseholdMemberRoleBlocksManagerOnlyAttr()
    {
        // HouseholdMinRole=Manager means only household Managers get the bypass — Members do not.
        await SeedAttributeAsync(tenantMinRole: (byte)TenantRole.Manager, householdMinRole: (byte)HouseholdRole.Manager);
        var result = await CreateService(TenantRole.Member, callerHouseholdRole: HouseholdRole.Member)
            .ListAsync(_tenantId, _householdId, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(result.Successful);
        Assert.Empty(result.Entity!);
    }

    [Fact]
    public async Task ListAsync_HouseholdBypass_HouseholdManagerBypassesManagerTenantRole()
    {
        await SeedAttributeAsync(tenantMinRole: (byte)TenantRole.Manager, householdMinRole: (byte)HouseholdRole.Manager);
        var result = await CreateService(TenantRole.Member, callerHouseholdRole: HouseholdRole.Manager)
            .ListAsync(_tenantId, _householdId, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(result.Successful);
        Assert.Single(result.Entity!);
    }

    // ── GetAsync visibility ──────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_InsufficientRoleAndNotInHousehold_ReturnsError()
    {
        var attr = await SeedAttributeAsync(tenantMinRole: (byte)TenantRole.Manager, householdMinRole: null);
        var result = await CreateService(TenantRole.Member)
            .GetAsync(_tenantId, _householdId, attr.Id, TestContext.Current.CancellationToken);
        Assert.False(result.Successful);
        Assert.Contains(result.Messages, m => m.Type == MessageType.ERROR);
    }

    [Fact]
    public async Task GetAsync_HouseholdBypass_ReturnsDto()
    {
        var attr = await SeedAttributeAsync(key: "Secret", tenantMinRole: (byte)TenantRole.Manager, householdMinRole: (byte)HouseholdRole.Member);
        var result = await CreateService(TenantRole.Member, callerHouseholdRole: HouseholdRole.Member)
            .GetAsync(_tenantId, _householdId, attr.Id, TestContext.Current.CancellationToken);
        Assert.True(result.Successful);
        Assert.Equal("Secret", result.Entity!.Key);
    }

    // ── Write authorization ──────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_HouseholdManager_CreatesAttribute()
    {
        var request = new CreateHouseholdAttributeRequest
        {
            Key = "Color",
            Value = "Red",
            TenantMinRole = (byte)TenantRole.Member,
        };
        var result = await CreateService(TenantRole.Member, canManageHousehold: true)
            .CreateAsync(_tenantId, _householdId, request, TestContext.Current.CancellationToken);
        Assert.True(result.Successful);
        Assert.Equal("Color", result.Entity!.Key);
    }

    [Fact]
    public async Task CreateAsync_Unauthorized_ReturnsError()
    {
        var request = new CreateHouseholdAttributeRequest { Key = "Color", Value = "Red" };
        var result = await CreateService(TenantRole.Member, canManageHousehold: false)
            .CreateAsync(_tenantId, _householdId, request, TestContext.Current.CancellationToken);
        Assert.False(result.Successful);
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
        var result = await CreateService(TenantRole.Manager, canManageHousehold: true)
            .CreateAsync(_tenantId, _householdId, request, TestContext.Current.CancellationToken);
        Assert.True(result.Successful);
        Assert.Equal((byte)HouseholdRole.Member, result.Entity!.HouseholdMinRole);
    }
}
