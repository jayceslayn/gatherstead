using Gatherstead.Api.Contracts.TenantUsers;
using Gatherstead.Api.Security;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.TenantUsers;
using Gatherstead.Api.Tests.Fixtures;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Moq;

namespace Gatherstead.Api.Tests.Services;

public class TenantUserServiceTests : IAsyncLifetime
{
    private GathersteadDbContext _dbContext = null!;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _actorUserId = Guid.NewGuid();
    private readonly Guid _targetUserId = Guid.NewGuid();

    public async ValueTask InitializeAsync()
    {
        _dbContext = TestDbContextFactory.Create(tenantId: _tenantId, currentUserId: _actorUserId);
        _dbContext.Tenants.Add(new Tenant { Id = _tenantId, Name = "Test Tenant" });
        _dbContext.Users.Add(new User { Id = _actorUserId, ExternalId = "actor@test" });
        _dbContext.Users.Add(new User { Id = _targetUserId, ExternalId = "target@test" });
        await _dbContext.SaveChangesAsync();
    }

    public ValueTask DisposeAsync()
    {
        _dbContext.Dispose();
        return ValueTask.CompletedTask;
    }

    private TenantUserService CreateService(TenantRole? actorRole = null, bool isAppAdmin = false)
    {
        var tenantContext = Mock.Of<ICurrentTenantContext>(c => c.TenantId == _tenantId);
        var userContext = Mock.Of<ICurrentUserContext>(c => c.UserId == _actorUserId);
        var appAdminContext = Mock.Of<IAppAdminContext>(c =>
            c.IsAppAdminAsync(It.IsAny<CancellationToken>()) == Task.FromResult<bool?>(isAppAdmin ? true : (bool?)false));

        var canManageTenant = actorRole.HasValue && actorRole.Value <= TenantRole.Manager;
        var authService = Mock.Of<IMemberAuthorizationService>(s =>
            s.CanManageTenantAsync(_tenantId, It.IsAny<CancellationToken>()) == Task.FromResult(canManageTenant || isAppAdmin));

        return new TenantUserService(_dbContext, tenantContext, userContext, authService, appAdminContext, new FakeAuthCache());
    }

    private async Task SeedTenantUserAsync(Guid userId, TenantRole role)
    {
        if (!_dbContext.TenantUsers.Local.Any(tu => tu.UserId == userId))
            _dbContext.TenantUsers.Add(new TenantUser { TenantId = _tenantId, UserId = userId, Role = role });
        await _dbContext.SaveChangesAsync();
    }

    // ── ListAsync ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task ListAsync_Manager_ReturnsAllUsers()
    {
        await SeedTenantUserAsync(_actorUserId, TenantRole.Manager);
        await SeedTenantUserAsync(_targetUserId, TenantRole.Member);
        var service = CreateService(TenantRole.Manager);

        var result = await service.ListAsync(_tenantId, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        Assert.Equal(2, result.Entity!.Count);
    }

    [Fact]
    public async Task ListAsync_Coordinator_ReturnsError()
    {
        await SeedTenantUserAsync(_actorUserId, TenantRole.Coordinator);
        var service = CreateService(TenantRole.Coordinator);

        var result = await service.ListAsync(_tenantId, TestContext.Current.CancellationToken);

        Assert.False(result.Successful);
        Assert.Contains(result.Messages, m => m.Type == Gatherstead.Api.Contracts.Responses.MessageType.ERROR);
    }

    [Fact]
    public async Task ListAsync_AppAdmin_ReturnsAllUsers()
    {
        await SeedTenantUserAsync(_targetUserId, TenantRole.Member);
        var service = CreateService(isAppAdmin: true);

        var result = await service.ListAsync(_tenantId, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
    }

    [Fact]
    public async Task ListAsync_IncludesExternalId()
    {
        await SeedTenantUserAsync(_targetUserId, TenantRole.Member);
        var service = CreateService(TenantRole.Manager);

        var result = await service.ListAsync(_tenantId, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        Assert.Contains(result.Entity!, dto => dto.ExternalId == "target@test");
    }

    [Fact]
    public async Task ListAsync_IncludesEmailAndDisplayName()
    {
        var target = await _dbContext.Users.FindAsync([_targetUserId], TestContext.Current.CancellationToken);
        target!.Email = "target@example.com";
        target.DisplayName = "Target User";
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        await SeedTenantUserAsync(_targetUserId, TenantRole.Member);
        var service = CreateService(TenantRole.Manager);

        var result = await service.ListAsync(_tenantId, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        var dto = Assert.Single(result.Entity!, d => d.UserId == _targetUserId);
        Assert.Equal("target@example.com", dto.Email);
        Assert.Equal("Target User", dto.DisplayName);
    }

    // ── UpdateRoleAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateRoleAsync_Manager_CanSetMemberRole()
    {
        await SeedTenantUserAsync(_actorUserId, TenantRole.Manager);
        await SeedTenantUserAsync(_targetUserId, TenantRole.Guest);
        var service = CreateService(TenantRole.Manager);

        var result = await service.UpdateRoleAsync(
            _tenantId, _targetUserId,
            new UpdateTenantUserRoleRequest(TenantRole.Member),
            TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        Assert.Equal(TenantRole.Member, result.Entity!.Role);
    }

    [Fact]
    public async Task UpdateRoleAsync_NonEscalation_Rejected()
    {
        // Manager (role=1) cannot grant Owner (role=0)
        await SeedTenantUserAsync(_actorUserId, TenantRole.Manager);
        await SeedTenantUserAsync(_targetUserId, TenantRole.Member);
        var service = CreateService(TenantRole.Manager);

        var result = await service.UpdateRoleAsync(
            _tenantId, _targetUserId,
            new UpdateTenantUserRoleRequest(TenantRole.Owner),
            TestContext.Current.CancellationToken);

        Assert.False(result.Successful);
        Assert.Contains(result.Messages, m => m.Type == Gatherstead.Api.Contracts.Responses.MessageType.ERROR);
    }

    [Fact]
    public async Task UpdateRoleAsync_LastOwner_CannotBeDemoted()
    {
        // Actor is the only Owner; trying to demote themselves
        await SeedTenantUserAsync(_actorUserId, TenantRole.Owner);
        var service = CreateService(TenantRole.Owner);

        // Create a Manager-level service manually since Owner passes CanManageTenant
        var tenantContext = Mock.Of<ICurrentTenantContext>(c => c.TenantId == _tenantId);
        var userContext = Mock.Of<ICurrentUserContext>(c => c.UserId == _actorUserId);
        var appAdminContext = Mock.Of<IAppAdminContext>(c =>
            c.IsAppAdminAsync(It.IsAny<CancellationToken>()) == Task.FromResult<bool?>((bool?)false));
        var authService = Mock.Of<IMemberAuthorizationService>(s =>
            s.CanManageTenantAsync(_tenantId, It.IsAny<CancellationToken>()) == Task.FromResult(true));
        var svc = new TenantUserService(_dbContext, tenantContext, userContext, authService, appAdminContext, new FakeAuthCache());

        var result = await svc.UpdateRoleAsync(
            _tenantId, _actorUserId,
            new UpdateTenantUserRoleRequest(TenantRole.Manager),
            TestContext.Current.CancellationToken);

        Assert.False(result.Successful);
        Assert.Contains(result.Messages, m =>
            m.Type == Gatherstead.Api.Contracts.Responses.MessageType.ERROR &&
            m.Message.Contains("last Owner"));
    }

    [Fact]
    public async Task UpdateRoleAsync_AppAdmin_CanGrantOwner()
    {
        await SeedTenantUserAsync(_targetUserId, TenantRole.Member);
        var service = CreateService(isAppAdmin: true);

        var result = await service.UpdateRoleAsync(
            _tenantId, _targetUserId,
            new UpdateTenantUserRoleRequest(TenantRole.Owner),
            TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        Assert.Equal(TenantRole.Owner, result.Entity!.Role);
    }

    [Fact]
    public async Task UpdateRoleAsync_Coordinator_ReturnsAuthError()
    {
        await SeedTenantUserAsync(_targetUserId, TenantRole.Member);
        var service = CreateService(TenantRole.Coordinator);

        var result = await service.UpdateRoleAsync(
            _tenantId, _targetUserId,
            new UpdateTenantUserRoleRequest(TenantRole.Guest),
            TestContext.Current.CancellationToken);

        Assert.False(result.Successful);
    }

    [Fact]
    public async Task UpdateRoleAsync_TargetNotFound_ReturnsError()
    {
        await SeedTenantUserAsync(_actorUserId, TenantRole.Manager);
        var service = CreateService(TenantRole.Manager);

        var result = await service.UpdateRoleAsync(
            _tenantId, Guid.NewGuid(),
            new UpdateTenantUserRoleRequest(TenantRole.Member),
            TestContext.Current.CancellationToken);

        Assert.False(result.Successful);
        Assert.Contains(result.Messages, m => m.Type == Gatherstead.Api.Contracts.Responses.MessageType.ERROR);
    }
}
