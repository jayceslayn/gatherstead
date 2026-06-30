using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Security;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.Tenants;
using Gatherstead.Api.Tests.Fixtures;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Moq;

namespace Gatherstead.Api.Tests.Services;

/// <summary>
/// Covers <see cref="TenantService.ListAsync"/>, which runs on the non-tenant-scoped
/// <c>GET /api/tenants</c> route (no <c>{tenantId}</c>), so the DbContext tenant context is null.
/// Before the named-filter fix the global tenant filter resolved to <c>TenantId == null</c> and hid
/// every membership; these tests pin that the tenant filter is bypassed while soft-delete stays on.
/// </summary>
public class TenantServiceTests : IAsyncLifetime
{
    private GathersteadDbContext _dbContext = null!;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _otherTenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _otherUserId = Guid.NewGuid();

    public async ValueTask InitializeAsync()
    {
        // Default null tenantId reproduces the no-tenant-context route. The auditing interceptor
        // skips its cross-tenant write guard when the context tenant is null, so we can seed rows
        // for any tenant directly.
        _dbContext = TestDbContextFactory.Create(currentUserId: _userId);
        _dbContext.Tenants.Add(new Tenant { Id = _tenantId, Name = "Acme" });
        _dbContext.Tenants.Add(new Tenant { Id = _otherTenantId, Name = "Other" });
        _dbContext.Users.Add(new User { Id = _userId, ExternalId = "user@test" });
        _dbContext.Users.Add(new User { Id = _otherUserId, ExternalId = "other@test" });
        await _dbContext.SaveChangesAsync();
    }

    public ValueTask DisposeAsync()
    {
        _dbContext.Dispose();
        return ValueTask.CompletedTask;
    }

    private TenantService CreateService()
    {
        var tenantContext = Mock.Of<ICurrentTenantContext>(c => c.TenantId == (Guid?)null);
        var appAdminContext = Mock.Of<IAppAdminContext>();
        var authService = Mock.Of<IMemberAuthorizationService>();
        var auditVisibility = Mock.Of<IAuditVisibilityContext>();
        return new TenantService(_dbContext, tenantContext, appAdminContext, authService, auditVisibility);
    }

    private async Task SeedMembershipAsync(Guid tenantId, Guid userId, TenantRole role, bool deleted = false)
    {
        var tu = new TenantUser { TenantId = tenantId, UserId = userId, Role = role };
        _dbContext.TenantUsers.Add(tu);
        await _dbContext.SaveChangesAsync();
        if (deleted)
        {
            tu.IsDeleted = true;
            await _dbContext.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task ListAsync_NoTenantContext_ReturnsUsersTenants()
    {
        // Regression: pre-fix this returned empty because the global tenant filter hid every row.
        await SeedMembershipAsync(_tenantId, _userId, TenantRole.Owner);

        var result = await CreateService().ListAsync(_userId, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        var tenant = Assert.Single(result.Entity!);
        Assert.Equal(_tenantId, tenant.Id);
        Assert.Equal("Acme", tenant.Name);
        Assert.Equal(TenantRole.Owner, tenant.UserRole);
    }

    [Fact]
    public async Task ListAsync_ExcludesSoftDeletedMembership()
    {
        // Soft-delete filter must remain active after the tenant filter is bypassed.
        await SeedMembershipAsync(_tenantId, _userId, TenantRole.Owner, deleted: true);

        var result = await CreateService().ListAsync(_userId, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        Assert.Empty(result.Entity!);
    }

    [Fact]
    public async Task ListAsync_ExcludesSoftDeletedTenant()
    {
        // The projected Tenant navigation must also respect the soft-delete filter.
        await SeedMembershipAsync(_tenantId, _userId, TenantRole.Owner);
        var tenant = await _dbContext.Tenants.FindAsync([_tenantId], TestContext.Current.CancellationToken);
        tenant!.IsDeleted = true;
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await CreateService().ListAsync(_userId, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        Assert.Empty(result.Entity!);
    }

    [Fact]
    public async Task ListAsync_OnlyReturnsCallersMemberships()
    {
        await SeedMembershipAsync(_tenantId, _userId, TenantRole.Owner);
        await SeedMembershipAsync(_otherTenantId, _otherUserId, TenantRole.Owner);

        var result = await CreateService().ListAsync(_userId, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        var tenant = Assert.Single(result.Entity!);
        Assert.Equal(_tenantId, tenant.Id);
    }

    [Fact]
    public async Task ListAsync_FiltersByIds()
    {
        await SeedMembershipAsync(_tenantId, _userId, TenantRole.Owner);
        await SeedMembershipAsync(_otherTenantId, _userId, TenantRole.Member);

        var result = await CreateService().ListAsync(
            _userId, ids: [_otherTenantId], cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        var tenant = Assert.Single(result.Entity!);
        Assert.Equal(_otherTenantId, tenant.Id);
    }
}
