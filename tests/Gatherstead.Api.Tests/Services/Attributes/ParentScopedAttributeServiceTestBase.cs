using Gatherstead.Api.Contracts.Attributes;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Services.Attributes;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Tests.Fixtures;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Moq;

namespace Gatherstead.Api.Tests.Services.Attributes;

public abstract class ParentScopedAttributeServiceTestBase<TEntity, TDto, TCreate, TUpdate, TService> : IAsyncLifetime
    where TEntity : AuditableEntity, IParentScopedAttribute, new()
    where TDto : IAttributeDto
    where TCreate : class, IAttributeWriteRequest
    where TUpdate : class, IAttributeWriteRequest
    where TService : IParentScopedAttributeService<TDto, TCreate, TUpdate>
{
    protected GathersteadDbContext DbContext { get; private set; } = null!;
    protected Guid TenantId { get; } = Guid.NewGuid();
    protected Guid ParentId { get; } = Guid.NewGuid();

    public virtual async ValueTask InitializeAsync()
    {
        DbContext = TestDbContextFactory.Create(tenantId: TenantId);
        DbContext.Tenants.Add(new Tenant { Id = TenantId, Name = "Test Tenant" });
        SeedParent(DbContext);
        await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public virtual ValueTask DisposeAsync()
    {
        DbContext.Dispose();
        return ValueTask.CompletedTask;
    }

    // ── Hooks ────────────────────────────────────────────────────────────────

    /// Add the parent entity (Property, Equipment, etc.) for the test tenant + parent IDs.
    protected abstract void SeedParent(GathersteadDbContext db);

    /// Build a new attribute entity (tenant + parent FK already set).
    protected abstract TEntity NewAttribute(string key = "TestKey", string value = "TestValue", byte tenantMinRole = (byte)TenantRole.Member);

    /// Build a Create request DTO.
    protected abstract TCreate NewCreateRequest(string key = "TestKey", string value = "TestValue", byte tenantMinRole = (byte)TenantRole.Member);

    /// Build an Update request DTO.
    protected abstract TUpdate NewUpdateRequest(string key = "TestKey", string value = "TestValue", byte tenantMinRole = (byte)TenantRole.Member);

    /// Build the service with auth mock configured for caller's tenant role + write permission.
    /// Concrete subclasses set up the appropriate auth gate (CanManageTenantAsync / CanManageEventAsync / CanManageHouseholdAsync).
    protected abstract TService MakeService(TenantRole? callerTenantRole, bool canManage);

    // ── Seed helper ──────────────────────────────────────────────────────────

    protected async Task<TEntity> SeedAttributeAsync(string key = "TestKey", string value = "TestValue", byte tenantMinRole = (byte)TenantRole.Member)
    {
        var entity = NewAttribute(key, value, tenantMinRole);
        DbContext.Add(entity);
        await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        return entity;
    }

    // ── ListAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ListAsync_MemberAttribute_VisibleToMember()
    {
        await SeedAttributeAsync(tenantMinRole: (byte)TenantRole.Member);
        var result = await MakeService(TenantRole.Member, canManage: false)
            .ListAsync(TenantId, ParentId, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(result.Successful);
        Assert.Single(result.Entity!);
    }

    [Fact]
    public async Task ListAsync_ManagerAttribute_HiddenFromMember()
    {
        await SeedAttributeAsync(tenantMinRole: (byte)TenantRole.Manager);
        var result = await MakeService(TenantRole.Member, canManage: false)
            .ListAsync(TenantId, ParentId, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(result.Successful);
        Assert.Empty(result.Entity!);
    }

    [Fact]
    public async Task ListAsync_ManagerAttribute_VisibleToManager()
    {
        await SeedAttributeAsync(tenantMinRole: (byte)TenantRole.Manager);
        var result = await MakeService(TenantRole.Manager, canManage: false)
            .ListAsync(TenantId, ParentId, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(result.Successful);
        Assert.Single(result.Entity!);
    }

    [Fact]
    public async Task ListAsync_Unauthenticated_ReturnsEmpty()
    {
        await SeedAttributeAsync(tenantMinRole: (byte)TenantRole.Member);
        var result = await MakeService(callerTenantRole: null, canManage: false)
            .ListAsync(TenantId, ParentId, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(result.Successful);
        Assert.Empty(result.Entity!);
    }

    [Fact]
    public async Task ListAsync_MixedVisibility_ReturnsOnlyVisible()
    {
        await SeedAttributeAsync(key: "Public", tenantMinRole: (byte)TenantRole.Member);
        await SeedAttributeAsync(key: "ManagerOnly", tenantMinRole: (byte)TenantRole.Manager);
        var result = await MakeService(TenantRole.Member, canManage: false)
            .ListAsync(TenantId, ParentId, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(result.Successful);
        Assert.Single(result.Entity!);
    }

    // ── GetAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_Visible_ReturnsDto()
    {
        var attr = await SeedAttributeAsync(tenantMinRole: (byte)TenantRole.Member);
        var result = await MakeService(TenantRole.Member, canManage: false)
            .GetAsync(TenantId, ParentId, attr.Id, TestContext.Current.CancellationToken);
        Assert.True(result.Successful);
        Assert.NotNull(result.Entity);
    }

    [Fact]
    public async Task GetAsync_InsufficientRole_ReturnsError()
    {
        var attr = await SeedAttributeAsync(tenantMinRole: (byte)TenantRole.Manager);
        var result = await MakeService(TenantRole.Member, canManage: false)
            .GetAsync(TenantId, ParentId, attr.Id, TestContext.Current.CancellationToken);
        Assert.False(result.Successful);
    }

    [Fact]
    public async Task GetAsync_NotFound_ReturnsError()
    {
        var result = await MakeService(TenantRole.Member, canManage: false)
            .GetAsync(TenantId, ParentId, Guid.NewGuid(), TestContext.Current.CancellationToken);
        Assert.False(result.Successful);
    }

    // ── CreateAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_Unauthorized_ReturnsError()
    {
        var result = await MakeService(TenantRole.Member, canManage: false)
            .CreateAsync(TenantId, ParentId, NewCreateRequest(), TestContext.Current.CancellationToken);
        Assert.False(result.Successful);
    }

    [Fact]
    public async Task CreateAsync_Authorized_CreatesAttribute()
    {
        var result = await MakeService(TenantRole.Manager, canManage: true)
            .CreateAsync(TenantId, ParentId, NewCreateRequest("Color", "Red"), TestContext.Current.CancellationToken);
        Assert.True(result.Successful);
        Assert.NotNull(result.Entity);
    }

    [Fact]
    public async Task CreateAsync_ParentNotFound_ReturnsError()
    {
        var result = await MakeService(TenantRole.Manager, canManage: true)
            .CreateAsync(TenantId, Guid.NewGuid(), NewCreateRequest(), TestContext.Current.CancellationToken);
        Assert.False(result.Successful);
    }

    [Fact]
    public async Task CreateAsync_DuplicateKey_ReturnsError()
    {
        await SeedAttributeAsync(key: "Color");
        var result = await MakeService(TenantRole.Manager, canManage: true)
            .CreateAsync(TenantId, ParentId, NewCreateRequest("Color", "Blue"), TestContext.Current.CancellationToken);
        Assert.False(result.Successful);
        Assert.Contains(result.Messages, m => m.Type == MessageType.ERROR && m.Message.Contains("Color"));
    }

    // ── UpdateAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_Authorized_UpdatesValue()
    {
        var attr = await SeedAttributeAsync(key: "Color", value: "Red");
        var result = await MakeService(TenantRole.Manager, canManage: true)
            .UpdateAsync(TenantId, ParentId, attr.Id, NewUpdateRequest("Color", "Blue"), TestContext.Current.CancellationToken);
        Assert.True(result.Successful);
    }

    [Fact]
    public async Task UpdateAsync_KeyConflictWithOtherAttribute_ReturnsError()
    {
        await SeedAttributeAsync(key: "Color");
        var other = await SeedAttributeAsync(key: "Size");
        var result = await MakeService(TenantRole.Manager, canManage: true)
            .UpdateAsync(TenantId, ParentId, other.Id, NewUpdateRequest("Color", "Large"), TestContext.Current.CancellationToken);
        Assert.False(result.Successful);
        Assert.Contains(result.Messages, m => m.Type == MessageType.ERROR && m.Message.Contains("Color"));
    }

    // ── DeleteAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_Authorized_SoftDeletes()
    {
        var attr = await SeedAttributeAsync();
        var result = await MakeService(TenantRole.Manager, canManage: true)
            .DeleteAsync(TenantId, ParentId, attr.Id, TestContext.Current.CancellationToken);
        Assert.True(result.Successful);
    }

    [Fact]
    public async Task DeleteAsync_Unauthorized_ReturnsError()
    {
        var attr = await SeedAttributeAsync();
        var result = await MakeService(TenantRole.Member, canManage: false)
            .DeleteAsync(TenantId, ParentId, attr.Id, TestContext.Current.CancellationToken);
        Assert.False(result.Successful);
    }

    // ── TenantContext mismatch ───────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_TenantContextMismatch_ReturnsError()
    {
        var differentTenantContext = Mock.Of<ICurrentTenantContext>(c => c.TenantId == Guid.NewGuid());
        var service = MakeServiceWithContext(differentTenantContext, canManage: true);
        var result = await service.CreateAsync(TenantId, ParentId, NewCreateRequest(), TestContext.Current.CancellationToken);
        Assert.False(result.Successful);
    }

    /// Build the service using a custom tenant context. Override to use the same auth setup as MakeService but
    /// substitute the tenant context.
    protected abstract TService MakeServiceWithContext(ICurrentTenantContext tenantContext, bool canManage);
}
