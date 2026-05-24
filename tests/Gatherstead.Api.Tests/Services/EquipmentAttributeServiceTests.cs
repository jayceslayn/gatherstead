using Gatherstead.Api.Contracts.EquipmentAttributes;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.EquipmentAttributes;
using Gatherstead.Api.Tests.Fixtures;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Moq;

namespace Gatherstead.Api.Tests.Services;

public class EquipmentAttributeServiceTests : IAsyncLifetime
{
    private GathersteadDbContext _dbContext = null!;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _equipmentId = Guid.NewGuid();

    public async ValueTask InitializeAsync()
    {
        _dbContext = TestDbContextFactory.Create(tenantId: _tenantId);
        _dbContext.Tenants.Add(new Tenant { Id = _tenantId, Name = "Test Tenant" });
        _dbContext.Equipment.Add(new Equipment { Id = _equipmentId, TenantId = _tenantId, Name = "Test Equipment" });
        await _dbContext.SaveChangesAsync();
    }

    public ValueTask DisposeAsync()
    {
        _dbContext.Dispose();
        return ValueTask.CompletedTask;
    }

    private EquipmentAttributeService CreateService(TenantRole? callerRole, bool canManage = false)
    {
        var tenantContext = Mock.Of<ICurrentTenantContext>(c => c.TenantId == _tenantId);
        var auth = new Mock<IMemberAuthorizationService>();
        auth.Setup(a => a.GetCallerTenantRoleAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(callerRole);
        auth.Setup(a => a.CanManageTenantAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(canManage);
        return new EquipmentAttributeService(_dbContext, tenantContext, auth.Object);
    }

    private async Task<EquipmentAttribute> SeedAttributeAsync(
        string key = "TestKey",
        string value = "TestValue",
        byte tenantMinRole = (byte)TenantRole.Member)
    {
        var attr = new EquipmentAttribute
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            EquipmentId = _equipmentId,
            Key = key,
            Value = value,
            TenantMinRole = tenantMinRole,
        };
        _dbContext.EquipmentAttributes.Add(attr);
        await _dbContext.SaveChangesAsync();
        return attr;
    }

    // ── ListAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ListAsync_MemberAttribute_VisibleToMember()
    {
        await SeedAttributeAsync(tenantMinRole: (byte)TenantRole.Member);
        var result = await CreateService(TenantRole.Member)
            .ListAsync(_tenantId, _equipmentId, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(result.Successful);
        Assert.Single(result.Entity!);
    }

    [Fact]
    public async Task ListAsync_CoordinatorAttribute_HiddenFromMember()
    {
        await SeedAttributeAsync(tenantMinRole: (byte)TenantRole.Coordinator);
        var result = await CreateService(TenantRole.Member)
            .ListAsync(_tenantId, _equipmentId, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(result.Successful);
        Assert.Empty(result.Entity!);
    }

    [Fact]
    public async Task ListAsync_CoordinatorAttribute_VisibleToCoordinator()
    {
        await SeedAttributeAsync(tenantMinRole: (byte)TenantRole.Coordinator);
        var result = await CreateService(TenantRole.Coordinator)
            .ListAsync(_tenantId, _equipmentId, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(result.Successful);
        Assert.Single(result.Entity!);
    }

    [Fact]
    public async Task ListAsync_Unauthenticated_ReturnsEmpty()
    {
        await SeedAttributeAsync(tenantMinRole: (byte)TenantRole.Member);
        var result = await CreateService(callerRole: null)
            .ListAsync(_tenantId, _equipmentId, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(result.Successful);
        Assert.Empty(result.Entity!);
    }

    [Fact]
    public async Task ListAsync_MixedVisibility_ReturnsOnlyVisible()
    {
        await SeedAttributeAsync(key: "Public", tenantMinRole: (byte)TenantRole.Member);
        await SeedAttributeAsync(key: "ManagerOnly", tenantMinRole: (byte)TenantRole.Manager);
        var result = await CreateService(TenantRole.Member)
            .ListAsync(_tenantId, _equipmentId, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(result.Successful);
        Assert.Single(result.Entity!);
        Assert.Equal("Public", result.Entity!.First().Key);
    }

    // ── GetAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_Visible_ReturnsDto()
    {
        var attr = await SeedAttributeAsync(key: "LockCode", tenantMinRole: (byte)TenantRole.Member);
        var result = await CreateService(TenantRole.Member)
            .GetAsync(_tenantId, _equipmentId, attr.Id, TestContext.Current.CancellationToken);
        Assert.True(result.Successful);
        Assert.Equal("LockCode", result.Entity!.Key);
    }

    [Fact]
    public async Task GetAsync_InsufficientRole_ReturnsError()
    {
        var attr = await SeedAttributeAsync(tenantMinRole: (byte)TenantRole.Manager);
        var result = await CreateService(TenantRole.Member)
            .GetAsync(_tenantId, _equipmentId, attr.Id, TestContext.Current.CancellationToken);
        Assert.False(result.Successful);
        Assert.Contains(result.Messages, m => m.Type == MessageType.ERROR);
    }

    [Fact]
    public async Task GetAsync_NotFound_ReturnsError()
    {
        var result = await CreateService(TenantRole.Member)
            .GetAsync(_tenantId, _equipmentId, Guid.NewGuid(), TestContext.Current.CancellationToken);
        Assert.False(result.Successful);
        Assert.Contains(result.Messages, m => m.Type == MessageType.ERROR);
    }

    // ── CreateAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_Unauthorized_ReturnsError()
    {
        var request = new CreateEquipmentAttributeRequest { Key = "Color", Value = "Red" };
        var result = await CreateService(TenantRole.Member, canManage: false)
            .CreateAsync(_tenantId, _equipmentId, request, TestContext.Current.CancellationToken);
        Assert.False(result.Successful);
    }

    [Fact]
    public async Task CreateAsync_Manager_CreatesAttribute()
    {
        var request = new CreateEquipmentAttributeRequest
        {
            Key = "Color",
            Value = "Red",
            TenantMinRole = (byte)TenantRole.Member,
        };
        var result = await CreateService(TenantRole.Manager, canManage: true)
            .CreateAsync(_tenantId, _equipmentId, request, TestContext.Current.CancellationToken);
        Assert.True(result.Successful);
        Assert.Equal("Color", result.Entity!.Key);
        Assert.Equal("Red", result.Entity.Value);
    }

    [Fact]
    public async Task CreateAsync_EquipmentNotFound_ReturnsError()
    {
        var request = new CreateEquipmentAttributeRequest { Key = "Color", Value = "Red", TenantMinRole = (byte)TenantRole.Member };
        var result = await CreateService(TenantRole.Manager, canManage: true)
            .CreateAsync(_tenantId, Guid.NewGuid(), request, TestContext.Current.CancellationToken);
        Assert.False(result.Successful);
        Assert.Contains(result.Messages, m => m.Type == MessageType.ERROR);
    }

    [Fact]
    public async Task CreateAsync_DuplicateKey_ReturnsError()
    {
        await SeedAttributeAsync(key: "Color");
        var request = new CreateEquipmentAttributeRequest { Key = "Color", Value = "Blue" };
        var result = await CreateService(TenantRole.Manager, canManage: true)
            .CreateAsync(_tenantId, _equipmentId, request, TestContext.Current.CancellationToken);
        Assert.False(result.Successful);
        Assert.Contains(result.Messages, m => m.Type == MessageType.ERROR && m.Message.Contains("Color"));
    }

    // ── UpdateAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_Manager_UpdatesValue()
    {
        var attr = await SeedAttributeAsync(key: "Color", value: "Red");
        var request = new UpdateEquipmentAttributeRequest
        {
            Key = "Color",
            Value = "Blue",
            TenantMinRole = (byte)TenantRole.Member,
        };
        var result = await CreateService(TenantRole.Manager, canManage: true)
            .UpdateAsync(_tenantId, _equipmentId, attr.Id, request, TestContext.Current.CancellationToken);
        Assert.True(result.Successful);
        Assert.Equal("Blue", result.Entity!.Value);
    }

    [Fact]
    public async Task UpdateAsync_KeyConflictWithOtherAttribute_ReturnsError()
    {
        await SeedAttributeAsync(key: "Color");
        var attr2 = await SeedAttributeAsync(key: "Size");
        var request = new UpdateEquipmentAttributeRequest { Key = "Color", Value = "Large" };
        var result = await CreateService(TenantRole.Manager, canManage: true)
            .UpdateAsync(_tenantId, _equipmentId, attr2.Id, request, TestContext.Current.CancellationToken);
        Assert.False(result.Successful);
        Assert.Contains(result.Messages, m => m.Type == MessageType.ERROR && m.Message.Contains("Color"));
    }

    // ── DeleteAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_Manager_SoftDeletes()
    {
        var attr = await SeedAttributeAsync();
        var result = await CreateService(TenantRole.Manager, canManage: true)
            .DeleteAsync(_tenantId, _equipmentId, attr.Id, TestContext.Current.CancellationToken);
        Assert.True(result.Successful);
        Assert.True(result.Entity!.IsDeleted);
    }

    [Fact]
    public async Task DeleteAsync_Unauthorized_ReturnsError()
    {
        var attr = await SeedAttributeAsync();
        var result = await CreateService(TenantRole.Member, canManage: false)
            .DeleteAsync(_tenantId, _equipmentId, attr.Id, TestContext.Current.CancellationToken);
        Assert.False(result.Successful);
    }

    [Fact]
    public async Task DeleteAsync_AlreadyDeleted_ReturnsWarning()
    {
        using var ctx = TestDbContextFactory.Create(tenantId: _tenantId, includeDeleted: true);
        ctx.Tenants.Add(new Tenant { Id = _tenantId, Name = "T" });
        var equipId = Guid.NewGuid();
        ctx.Equipment.Add(new Equipment { Id = equipId, TenantId = _tenantId, Name = "E" });
        var attrId = Guid.NewGuid();
        ctx.EquipmentAttributes.Add(new EquipmentAttribute
        {
            Id = attrId,
            TenantId = _tenantId,
            EquipmentId = equipId,
            Key = "K",
            Value = "V",
            IsDeleted = true,
        });
        await ctx.SaveChangesAsync();

        var tenantContext = Mock.Of<ICurrentTenantContext>(c => c.TenantId == _tenantId);
        var auth = Mock.Of<IMemberAuthorizationService>(a =>
            a.CanManageTenantAsync(_tenantId, It.IsAny<CancellationToken>()) == Task.FromResult(true));
        var service = new EquipmentAttributeService(ctx, tenantContext, auth);

        var result = await service.DeleteAsync(_tenantId, equipId, attrId, TestContext.Current.CancellationToken);
        Assert.False(result.Successful);
        Assert.Contains(result.Messages, m => m.Type == MessageType.WARNING);
    }
}
