using Gatherstead.Api.Contracts.Equipment;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.Equipment;
using Gatherstead.Api.Tests.Fixtures;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Gatherstead.Api.Tests.Services;

public class EquipmentServiceTests : IAsyncLifetime
{
    private GathersteadDbContext _dbContext = null!;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _propertyId = Guid.NewGuid();

    public async ValueTask InitializeAsync()
    {
        _dbContext = TestDbContextFactory.Create(tenantId: _tenantId);
        _dbContext.Tenants.Add(new Tenant { Id = _tenantId, Name = "Test Tenant" });
        _dbContext.Properties.Add(new Property { Id = _propertyId, TenantId = _tenantId, Name = "Test Property" });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        _dbContext.Dispose();
        return ValueTask.CompletedTask;
    }

    private EquipmentService CreateService(bool canManage = false, Guid? contextTenantId = null, TenantRole? callerRole = null)
    {
        var tenantContext = Mock.Of<ICurrentTenantContext>(c => c.TenantId == (contextTenantId ?? _tenantId));
        var auth = Mock.Of<IMemberAuthorizationService>(a =>
            a.CanManageTenantAsync(_tenantId, It.IsAny<CancellationToken>()) == Task.FromResult(canManage) &&
            a.GetCallerTenantRoleAsync(_tenantId, It.IsAny<CancellationToken>()) == Task.FromResult(callerRole));
        return new EquipmentService(_dbContext, tenantContext, auth,
            Mock.Of<Gatherstead.Api.Contracts.Responses.IAuditVisibilityContext>());
    }

    // ── ValidateTenantContext ─────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_TenantContextMismatch_ReturnsError()
    {
        var request = new CreateEquipmentRequest { Name = "Ladder" };
        var result = await CreateService(canManage: true, contextTenantId: Guid.NewGuid())
            .CreateAsync(_tenantId, request, TestContext.Current.CancellationToken);
        Assert.False(result.Successful);
        Assert.Contains(result.Messages, m => m.Type == MessageType.ERROR);
    }

    // ── CreateAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_Unauthorized_ReturnsError()
    {
        var request = new CreateEquipmentRequest { Name = "Ladder" };
        var result = await CreateService(canManage: false)
            .CreateAsync(_tenantId, request, TestContext.Current.CancellationToken);
        Assert.False(result.Successful);
    }

    [Fact]
    public async Task CreateAsync_NullPropertyId_CreatesEquipment()
    {
        var request = new CreateEquipmentRequest { Name = "Portable Generator", PropertyId = null };
        var result = await CreateService(canManage: true)
            .CreateAsync(_tenantId, request, TestContext.Current.CancellationToken);
        Assert.True(result.Successful);
        Assert.Null(result.Entity!.PropertyId);
    }

    [Fact]
    public async Task UpdateAsync_AppAdmin_PreservesAttributesItCannotSee()
    {
        // App-admin caller: write authority granted (canManage) but no tenant role, so the unconfigured
        // GetCallerTenantRoleAsync mock returns null — exactly how an app admin resolves. A role-gated
        // attribute they cannot see must survive a full-replace update that omits it (no blanking).
        var equipmentId = Guid.NewGuid();
        var attributeId = Guid.NewGuid();
        _dbContext.Equipment.Add(new Equipment { Id = equipmentId, TenantId = _tenantId, Name = "Generator" });
        _dbContext.EquipmentAttributes.Add(new EquipmentAttribute
        {
            Id = attributeId,
            TenantId = _tenantId,
            EquipmentId = equipmentId,
            Key = "serialNumber",
            Value = "SN-SECRET",
            TenantMinRole = (byte)TenantRole.Manager,
        });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Full-replace update with an empty attribute list (the app admin never received the hidden attr).
        var request = new UpdateEquipmentRequest { Name = "Generator", Attributes = [] };
        var result = await CreateService(canManage: true)
            .UpdateAsync(_tenantId, equipmentId, request, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        var attr = await _dbContext.EquipmentAttributes
            .IgnoreQueryFilters()
            .SingleAsync(a => a.Id == attributeId, TestContext.Current.CancellationToken);
        Assert.False(attr.IsDeleted);
        Assert.Equal("SN-SECRET", attr.Value);
    }

    [Fact]
    public async Task CreateAsync_ValidPropertyId_CreatesEquipment()
    {
        var request = new CreateEquipmentRequest { Name = "Lawn Mower", PropertyId = _propertyId };
        var result = await CreateService(canManage: true)
            .CreateAsync(_tenantId, request, TestContext.Current.CancellationToken);
        Assert.True(result.Successful);
        Assert.Equal(_propertyId, result.Entity!.PropertyId);
    }

    [Fact]
    public async Task CreateAsync_PropertyIdNotFound_ReturnsError()
    {
        var request = new CreateEquipmentRequest { Name = "Ladder", PropertyId = Guid.NewGuid() };
        var result = await CreateService(canManage: true)
            .CreateAsync(_tenantId, request, TestContext.Current.CancellationToken);
        Assert.False(result.Successful);
        Assert.Contains(result.Messages, m => m.Type == MessageType.ERROR);
    }

    [Fact]
    public async Task CreateAsync_DuplicateName_ReturnsError()
    {
        _dbContext.Equipment.Add(new Equipment { Id = Guid.NewGuid(), TenantId = _tenantId, Name = "Tractor" });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var request = new CreateEquipmentRequest { Name = "Tractor" };
        var result = await CreateService(canManage: true)
            .CreateAsync(_tenantId, request, TestContext.Current.CancellationToken);
        Assert.False(result.Successful);
        Assert.Contains(result.Messages, m => m.Type == MessageType.ERROR);
    }

    // ── UpdateAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_ValidPropertyId_UpdatesEquipment()
    {
        var equipId = Guid.NewGuid();
        _dbContext.Equipment.Add(new Equipment { Id = equipId, TenantId = _tenantId, Name = "Old Name" });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var request = new UpdateEquipmentRequest { Name = "New Name", PropertyId = _propertyId };
        var result = await CreateService(canManage: true)
            .UpdateAsync(_tenantId, equipId, request, TestContext.Current.CancellationToken);
        Assert.True(result.Successful);
        Assert.Equal(_propertyId, result.Entity!.PropertyId);
        Assert.Equal("New Name", result.Entity.Name);
    }

    [Fact]
    public async Task UpdateAsync_PropertyIdNotFound_ReturnsError()
    {
        var equipId = Guid.NewGuid();
        _dbContext.Equipment.Add(new Equipment { Id = equipId, TenantId = _tenantId, Name = "Ladder" });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var request = new UpdateEquipmentRequest { Name = "Ladder", PropertyId = Guid.NewGuid() };
        var result = await CreateService(canManage: true)
            .UpdateAsync(_tenantId, equipId, request, TestContext.Current.CancellationToken);
        Assert.False(result.Successful);
        Assert.Contains(result.Messages, m => m.Type == MessageType.ERROR);
    }

    [Fact]
    public async Task UpdateAsync_NotFound_ReturnsError()
    {
        var request = new UpdateEquipmentRequest { Name = "Ghost" };
        var result = await CreateService(canManage: true)
            .UpdateAsync(_tenantId, Guid.NewGuid(), request, TestContext.Current.CancellationToken);
        Assert.False(result.Successful);
        Assert.Contains(result.Messages, m => m.Type == MessageType.ERROR);
    }

    // ── ListAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task ListAsync_ReturnsEquipmentForTenant()
    {
        // Guard: this List already materializes before mapping; keep it exercised under SQLite so it
        // cannot regress into an untranslatable instance-method projection.
        _dbContext.Equipment.Add(new Equipment { Id = Guid.NewGuid(), TenantId = _tenantId, Name = "Tractor" });
        _dbContext.Equipment.Add(new Equipment { Id = Guid.NewGuid(), TenantId = _tenantId, Name = "Ladder" });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await CreateService().ListAsync(_tenantId, null, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        Assert.Equal(2, result.Entity!.Count);
    }

    [Fact]
    public async Task ListAsync_OrdersByPropertyThenName_UnassignedLast()
    {
        // "Alpine Lodge" sorts before the fixture's "Test Property"; unassigned equipment sorts last
        // even though "Aaa Gear" is alphabetically first by name.
        var alpineId = Guid.NewGuid();
        _dbContext.Properties.Add(new Property { Id = alpineId, TenantId = _tenantId, Name = "Alpine Lodge" });
        _dbContext.Equipment.AddRange(
            new Equipment { Id = Guid.NewGuid(), TenantId = _tenantId, Name = "Aaa Gear", PropertyId = null },
            new Equipment { Id = Guid.NewGuid(), TenantId = _tenantId, Name = "Zzz Rake", PropertyId = _propertyId },
            new Equipment { Id = Guid.NewGuid(), TenantId = _tenantId, Name = "Mop", PropertyId = alpineId },
            new Equipment { Id = Guid.NewGuid(), TenantId = _tenantId, Name = "Axe", PropertyId = _propertyId });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await CreateService().ListAsync(_tenantId, null, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        // Alpine Lodge (Mop), then Test Property by name (Axe, Zzz Rake), then unassigned (Aaa Gear).
        Assert.Equal(["Mop", "Axe", "Zzz Rake", "Aaa Gear"], result.Entity!.Select(e => e.Name));
    }

    [Fact]
    public async Task ListAsync_IncludesVisibleAttributes_FiltersHiddenByRole()
    {
        // Lists now carry visible attributes (mirrors GetAsync) so cards render them and an edit that
        // re-sends the list-sourced attributes doesn't wipe the inventory. This tenant-role visibility
        // path is shared by every attribute-bearing list endpoint; equipment stands in for them all.
        var equipmentId = Guid.NewGuid();
        _dbContext.Equipment.Add(new Equipment { Id = equipmentId, TenantId = _tenantId, Name = "Generator" });
        _dbContext.EquipmentAttributes.AddRange(
            new EquipmentAttribute
            {
                Id = Guid.NewGuid(), TenantId = _tenantId, EquipmentId = equipmentId,
                Key = "fuel", Value = "diesel", TenantMinRole = (byte)TenantRole.Member,
            },
            new EquipmentAttribute
            {
                Id = Guid.NewGuid(), TenantId = _tenantId, EquipmentId = equipmentId,
                Key = "serialNumber", Value = "SN-SECRET", TenantMinRole = (byte)TenantRole.Owner,
            });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Caller is a Member: sees the Member-gated attribute, not the Owner-gated one.
        var result = await CreateService(callerRole: TenantRole.Member)
            .ListAsync(_tenantId, null, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        var listed = Assert.Single(result.Entity!);
        var attr = Assert.Single(listed.Attributes);
        Assert.Equal("fuel", attr.Key);
    }
}
