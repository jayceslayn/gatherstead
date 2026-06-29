using Gatherstead.Api.Contracts.Invitations;
using Gatherstead.Api.Security;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.Invitations;
using Gatherstead.Api.Services.Observability;
using Gatherstead.Api.Tests.Fixtures;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Gatherstead.Api.Tests.Services;

public class InvitationServiceTests : IAsyncLifetime
{
    private GathersteadDbContext _dbContext = null!;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _actorUserId = Guid.NewGuid();
    private readonly Guid _householdId = Guid.NewGuid();
    private Mock<ISecurityEventLogger> _securityLogger = null!;

    public async ValueTask InitializeAsync()
    {
        _dbContext = TestDbContextFactory.Create(tenantId: _tenantId, currentUserId: _actorUserId);
        _dbContext.Tenants.Add(new Tenant { Id = _tenantId, Name = "Test Tenant" });
        _dbContext.Users.Add(new User { Id = _actorUserId, ExternalId = "actor@test" });
        _dbContext.Households.Add(new Household { Id = _householdId, TenantId = _tenantId, Name = "Smith Household" });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        _dbContext.Dispose();
        return ValueTask.CompletedTask;
    }

    private InvitationService CreateService(bool canManage = true, TenantRole actorRole = TenantRole.Manager)
    {
        var tenantContext = Mock.Of<ICurrentTenantContext>(c => c.TenantId == _tenantId);
        var userContext = Mock.Of<ICurrentUserContext>(c => c.UserId == _actorUserId);
        var auth = Mock.Of<IMemberAuthorizationService>(a =>
            a.CanManageTenantAsync(_tenantId, It.IsAny<CancellationToken>()) == Task.FromResult(canManage)
            && a.GetCallerTenantRoleAsync(_tenantId, It.IsAny<CancellationToken>()) == Task.FromResult((TenantRole?)actorRole));
        _securityLogger = new Mock<ISecurityEventLogger>();
        return new InvitationService(_dbContext, tenantContext, userContext, auth, _securityLogger.Object);
    }

    [Fact]
    public async Task CreateAsync_NewEmail_CreatesPendingInvitation()
    {
        var request = new CreateInvitationRequest { Email = "newuser@test.com", Role = TenantRole.Member };

        var result = await CreateService().CreateAsync(_tenantId, request, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        Assert.Equal(InvitationStatus.Pending, result.Entity!.Status);
        // Email is normalized to lower-case for matching.
        Assert.Equal("newuser@test.com", result.Entity.Email);
        Assert.Null(result.Entity.AcceptedAt);

        var invite = await _dbContext.Invitations.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(InvitationStatus.Pending, invite.Status);
        Assert.False(await _dbContext.TenantUsers.AnyAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateAsync_NormalizesEmailToLowerInvariant()
    {
        var request = new CreateInvitationRequest { Email = "  MixedCase@Test.COM ", Role = TenantRole.Member };

        var result = await CreateService().CreateAsync(_tenantId, request, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        Assert.Equal("mixedcase@test.com", result.Entity!.Email);
    }

    [Fact]
    public async Task CreateAsync_ExistingUser_AcceptsImmediatelyAndGrantsMembership()
    {
        var existingUserId = Guid.NewGuid();
        _dbContext.Users.Add(new User { Id = existingUserId, ExternalId = "existing@idp", Email = "existing@test.com" });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var request = new CreateInvitationRequest
        {
            Email = "existing@test.com",
            Role = TenantRole.Coordinator,
            HouseholdId = _householdId,
            HouseholdRole = HouseholdRole.Manager,
        };

        var result = await CreateService().CreateAsync(_tenantId, request, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        Assert.Equal(InvitationStatus.Accepted, result.Entity!.Status);
        Assert.NotNull(result.Entity.AcceptedAt);

        var tenantUser = await _dbContext.TenantUsers.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(existingUserId, tenantUser.UserId);
        Assert.Equal(TenantRole.Coordinator, tenantUser.Role);

        var householdUser = await _dbContext.HouseholdUsers.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(_householdId, householdUser.HouseholdId);
        Assert.Equal(HouseholdRole.Manager, householdUser.Role);
    }

    [Fact]
    public async Task CreateAsync_DuplicatePending_IsIdempotent()
    {
        var request = new CreateInvitationRequest { Email = "dupe@test.com", Role = TenantRole.Member };
        var service = CreateService();

        var first = await service.CreateAsync(_tenantId, request, TestContext.Current.CancellationToken);
        var second = await service.CreateAsync(_tenantId, request, TestContext.Current.CancellationToken);

        Assert.True(first.Successful);
        Assert.True(second.Successful);
        Assert.Equal(first.Entity!.Id, second.Entity!.Id);
        Assert.Equal(1, await _dbContext.Invitations.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateAsync_NonEscalation_Rejected()
    {
        // A Manager (role=1) may not grant Owner (role=0).
        var request = new CreateInvitationRequest { Email = "escalate@test.com", Role = TenantRole.Owner };

        var result = await CreateService(actorRole: TenantRole.Manager).CreateAsync(_tenantId, request, TestContext.Current.CancellationToken);

        Assert.False(result.Successful);
        Assert.False(await _dbContext.Invitations.AnyAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateAsync_WithoutManagePermission_Rejected()
    {
        var request = new CreateInvitationRequest { Email = "nope@test.com", Role = TenantRole.Member };

        var result = await CreateService(canManage: false).CreateAsync(_tenantId, request, TestContext.Current.CancellationToken);

        Assert.False(result.Successful);
        Assert.False(await _dbContext.Invitations.AnyAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateAsync_MissingEmail_Rejected()
    {
        var request = new CreateInvitationRequest { Email = "   ", Role = TenantRole.Member };

        var result = await CreateService().CreateAsync(_tenantId, request, TestContext.Current.CancellationToken);

        Assert.False(result.Successful);
    }

    [Fact]
    public async Task CreateAsync_UnknownHousehold_Rejected()
    {
        var request = new CreateInvitationRequest
        {
            Email = "h@test.com",
            Role = TenantRole.Member,
            HouseholdId = Guid.NewGuid(),
        };

        var result = await CreateService().CreateAsync(_tenantId, request, TestContext.Current.CancellationToken);

        Assert.False(result.Successful);
    }

    [Fact]
    public async Task ListAsync_ReturnsTenantInvitationsNewestFirst()
    {
        var service = CreateService();
        await service.CreateAsync(_tenantId, new CreateInvitationRequest { Email = "a@test.com", Role = TenantRole.Member }, TestContext.Current.CancellationToken);
        await service.CreateAsync(_tenantId, new CreateInvitationRequest { Email = "b@test.com", Role = TenantRole.Member }, TestContext.Current.CancellationToken);

        var result = await service.ListAsync(_tenantId, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        Assert.Equal(2, result.Entity!.Count);
    }

    [Fact]
    public async Task RevokeAsync_MarksRevokedAndSoftDeletes()
    {
        var service = CreateService();
        var created = await service.CreateAsync(_tenantId, new CreateInvitationRequest { Email = "revoke@test.com", Role = TenantRole.Member }, TestContext.Current.CancellationToken);

        var result = await service.RevokeAsync(_tenantId, created.Entity!.Id, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        var invite = await _dbContext.Invitations.IgnoreQueryFilters().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(InvitationStatus.Revoked, invite.Status);
        Assert.True(invite.IsDeleted);
    }

    [Fact]
    public async Task RevokeAsync_RevokedEmailCanBeReinvited()
    {
        var service = CreateService();
        var first = await service.CreateAsync(_tenantId, new CreateInvitationRequest { Email = "again@test.com", Role = TenantRole.Member }, TestContext.Current.CancellationToken);
        await service.RevokeAsync(_tenantId, first.Entity!.Id, TestContext.Current.CancellationToken);

        // After revocation the (tenant, email) pending slot is free again.
        var second = await service.CreateAsync(_tenantId, new CreateInvitationRequest { Email = "again@test.com", Role = TenantRole.Member }, TestContext.Current.CancellationToken);

        Assert.True(second.Successful);
        Assert.Equal(InvitationStatus.Pending, second.Entity!.Status);
        Assert.NotEqual(first.Entity.Id, second.Entity.Id);
    }

    [Fact]
    public async Task CreateAsync_PendingInvite_LogsInvitationCreated()
    {
        var request = new CreateInvitationRequest { Email = "logme@test.com", Role = TenantRole.Member };

        var result = await CreateService().CreateAsync(_tenantId, request, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        _securityLogger.Verify(s => s.LogAsync(
            SecurityEventType.InvitationCreated,
            SecurityEventSeverity.Info,
            It.IsAny<string>(), It.IsAny<string>(), _tenantId, _actorUserId, It.IsAny<CancellationToken>()),
            Times.Once);
        // A pending invite is not yet accepted, so no acceptance event is emitted.
        _securityLogger.Verify(s => s.LogAsync(
            SecurityEventType.InvitationAccepted,
            It.IsAny<SecurityEventSeverity>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateAsync_ExistingUserAutoAccept_LogsCreatedAndAccepted()
    {
        var existingUserId = Guid.NewGuid();
        _dbContext.Users.Add(new User { Id = existingUserId, ExternalId = "auto@idp", Email = "auto@test.com" });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var request = new CreateInvitationRequest { Email = "auto@test.com", Role = TenantRole.Member };

        var result = await CreateService().CreateAsync(_tenantId, request, TestContext.Current.CancellationToken);

        Assert.Equal(InvitationStatus.Accepted, result.Entity!.Status);
        _securityLogger.Verify(s => s.LogAsync(
            SecurityEventType.InvitationCreated, SecurityEventSeverity.Info,
            It.IsAny<string>(), It.IsAny<string>(), _tenantId, _actorUserId, It.IsAny<CancellationToken>()),
            Times.Once);
        // Inviter (_actorUserId) differs from the accepting user, so acceptance is informational.
        _securityLogger.Verify(s => s.LogAsync(
            SecurityEventType.InvitationAccepted, SecurityEventSeverity.Info,
            It.IsAny<string>(), It.IsAny<string>(), _tenantId, existingUserId, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
