using Gatherstead.Api.Contracts.TenantUsers;
using Gatherstead.Api.Security;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.Observability;
using Gatherstead.Api.Services.TenantUsers;
using Gatherstead.Api.Tests.Fixtures;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;
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
            s.CanManageTenantAsync(_tenantId, It.IsAny<CancellationToken>()) == Task.FromResult(canManageTenant || isAppAdmin)
            && s.CanManageHouseholdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()) == Task.FromResult(canManageTenant || isAppAdmin));

        return new TenantUserService(_dbContext, tenantContext, userContext, authService, appAdminContext, new FakeAuthCache(), Mock.Of<ISecurityEventLogger>());
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
        var svc = new TenantUserService(_dbContext, tenantContext, userContext, authService, appAdminContext, new FakeAuthCache(), Mock.Of<ISecurityEventLogger>());

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

    // ── RemoveAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveAsync_SoftDeletesMembership_ClearsLink_AndCascadesHouseholdAccess()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedTenantUserAsync(_actorUserId, TenantRole.Owner);
        await SeedTenantUserAsync(_targetUserId, TenantRole.Member);

        var householdId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        _dbContext.Households.Add(new Household { Id = householdId, TenantId = _tenantId, Name = "House" });
        _dbContext.HouseholdMembers.Add(new HouseholdMember { Id = memberId, TenantId = _tenantId, HouseholdId = householdId, Name = "Alice" });
        var target = await _dbContext.TenantUsers.SingleAsync(tu => tu.UserId == _targetUserId, ct);
        target.LinkedMemberId = memberId;
        _dbContext.HouseholdUsers.Add(new HouseholdUser { TenantId = _tenantId, HouseholdId = householdId, UserId = _targetUserId, Role = HouseholdRole.Member });
        await _dbContext.SaveChangesAsync(ct);

        var result = await CreateService(TenantRole.Owner).RemoveAsync(_tenantId, _targetUserId, ct);

        Assert.True(result.Successful);
        var removed = await _dbContext.TenantUsers.IgnoreQueryFilters()
            .SingleAsync(tu => tu.TenantId == _tenantId && tu.UserId == _targetUserId, ct);
        Assert.True(removed.IsDeleted);
        Assert.Null(removed.LinkedMemberId);
        var hu = await _dbContext.HouseholdUsers.IgnoreQueryFilters()
            .SingleAsync(x => x.HouseholdId == householdId && x.UserId == _targetUserId, ct);
        Assert.True(hu.IsDeleted);
    }

    [Fact]
    public async Task RemoveAsync_OwnerCanRemoveAnotherOwner()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedTenantUserAsync(_actorUserId, TenantRole.Owner);
        await SeedTenantUserAsync(_targetUserId, TenantRole.Owner);

        var result = await CreateService(TenantRole.Owner).RemoveAsync(_tenantId, _targetUserId, ct);

        Assert.True(result.Successful);
        var removed = await _dbContext.TenantUsers.IgnoreQueryFilters()
            .SingleAsync(tu => tu.UserId == _targetUserId, ct);
        Assert.True(removed.IsDeleted);
    }

    [Fact]
    public async Task RemoveAsync_SelfRemoval_Blocked()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedTenantUserAsync(_actorUserId, TenantRole.Owner);

        var result = await CreateService(TenantRole.Owner).RemoveAsync(_tenantId, _actorUserId, ct);

        Assert.False(result.Successful);
        var self = await _dbContext.TenantUsers.SingleAsync(tu => tu.UserId == _actorUserId, ct);
        Assert.False(self.IsDeleted);
    }

    [Fact]
    public async Task RemoveAsync_RemovingMorePrivilegedUser_Blocked()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedTenantUserAsync(_actorUserId, TenantRole.Manager);
        await SeedTenantUserAsync(_targetUserId, TenantRole.Owner);

        var result = await CreateService(TenantRole.Manager).RemoveAsync(_tenantId, _targetUserId, ct);

        Assert.False(result.Successful);
        var target = await _dbContext.TenantUsers.SingleAsync(tu => tu.UserId == _targetUserId, ct);
        Assert.False(target.IsDeleted);
    }

    [Fact]
    public async Task RemoveAsync_WithoutManagePermission_Blocked()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedTenantUserAsync(_targetUserId, TenantRole.Member);

        // Coordinator cannot manage tenant users.
        var result = await CreateService(TenantRole.Coordinator).RemoveAsync(_tenantId, _targetUserId, ct);

        Assert.False(result.Successful);
        var target = await _dbContext.TenantUsers.SingleAsync(tu => tu.UserId == _targetUserId, ct);
        Assert.False(target.IsDeleted);
    }

    [Fact]
    public async Task RemoveAsync_UnknownUser_ReturnsError()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedTenantUserAsync(_actorUserId, TenantRole.Owner);

        var result = await CreateService(TenantRole.Owner).RemoveAsync(_tenantId, Guid.NewGuid(), ct);

        Assert.False(result.Successful);
    }

    [Fact]
    public async Task RemoveAsync_AppAdmin_BypassesRoleGuards_AndAudits()
    {
        var ct = TestContext.Current.CancellationToken;
        // App Admin is not a tenant member and bypasses self/escalation/last-owner guards.
        await SeedTenantUserAsync(_targetUserId, TenantRole.Owner);

        var tenantContext = Mock.Of<ICurrentTenantContext>(c => c.TenantId == _tenantId);
        var userContext = Mock.Of<ICurrentUserContext>(c => c.UserId == _actorUserId);
        var appAdminContext = Mock.Of<IAppAdminContext>(c =>
            c.IsAppAdminAsync(It.IsAny<CancellationToken>()) == Task.FromResult<bool?>(true));
        var authService = Mock.Of<IMemberAuthorizationService>(s =>
            s.CanManageTenantAsync(_tenantId, It.IsAny<CancellationToken>()) == Task.FromResult(true));
        var logger = new Mock<ISecurityEventLogger>();
        var svc = new TenantUserService(_dbContext, tenantContext, userContext, authService, appAdminContext, new FakeAuthCache(), logger.Object);

        var result = await svc.RemoveAsync(_tenantId, _targetUserId, ct);

        Assert.True(result.Successful);
        var removed = await _dbContext.TenantUsers.IgnoreQueryFilters()
            .SingleAsync(tu => tu.UserId == _targetUserId, ct);
        Assert.True(removed.IsDeleted);
        logger.Verify(s => s.LogAsync(
            SecurityEventType.AppAdminAction, SecurityEventSeverity.Info,
            It.IsAny<string>(), It.IsAny<string>(), _tenantId, _actorUserId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── SetLinkedMemberAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task SetLinkedMemberAsync_LinksFreeMember()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedTenantUserAsync(_actorUserId, TenantRole.Manager);
        await SeedTenantUserAsync(_targetUserId, TenantRole.Member);
        var memberId = await SeedMemberAsync();

        var result = await CreateService(TenantRole.Manager).SetLinkedMemberAsync(
            _tenantId, _targetUserId, new SetLinkedMemberRequest { MemberId = memberId }, ct);

        Assert.True(result.Successful);
        var target = await _dbContext.TenantUsers.SingleAsync(tu => tu.UserId == _targetUserId, ct);
        Assert.Equal(memberId, target.LinkedMemberId);
    }

    [Fact]
    public async Task SetLinkedMemberAsync_MemberPromisedToPendingInvitation_Rejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedTenantUserAsync(_actorUserId, TenantRole.Manager);
        await SeedTenantUserAsync(_targetUserId, TenantRole.Member);
        var memberId = await SeedMemberAsync();
        _dbContext.Invitations.Add(new Invitation
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, Email = "promised@test.com",
            Role = TenantRole.Member, Status = InvitationStatus.Pending, LinkedMemberId = memberId,
        });
        await _dbContext.SaveChangesAsync(ct);

        // A pending invitation is an outstanding promise of the link; linking the member directly
        // would silently strand the invitee's link at accept time.
        var result = await CreateService(TenantRole.Manager).SetLinkedMemberAsync(
            _tenantId, _targetUserId, new SetLinkedMemberRequest { MemberId = memberId }, ct);

        Assert.False(result.Successful);
        var target = await _dbContext.TenantUsers.SingleAsync(tu => tu.UserId == _targetUserId, ct);
        Assert.Null(target.LinkedMemberId);
    }

    private async Task<Guid> SeedMemberAsync()
    {
        var householdId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        _dbContext.Households.Add(new Household { Id = householdId, TenantId = _tenantId, Name = "House" });
        _dbContext.HouseholdMembers.Add(new HouseholdMember
        {
            Id = memberId, TenantId = _tenantId, HouseholdId = householdId, Name = "Alice",
        });
        await _dbContext.SaveChangesAsync();
        return memberId;
    }
}
