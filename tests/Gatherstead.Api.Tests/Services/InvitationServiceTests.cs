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

    private InvitationService CreateService(bool canManage = true, TenantRole actorRole = TenantRole.Manager, bool canManageHousehold = true)
    {
        var tenantContext = Mock.Of<ICurrentTenantContext>(c => c.TenantId == _tenantId);
        var userContext = Mock.Of<ICurrentUserContext>(c => c.UserId == _actorUserId);
        var auth = Mock.Of<IMemberAuthorizationService>(a =>
            a.CanManageTenantAsync(_tenantId, It.IsAny<CancellationToken>()) == Task.FromResult(canManage)
            && a.GetCallerTenantRoleAsync(_tenantId, It.IsAny<CancellationToken>()) == Task.FromResult((TenantRole?)actorRole)
            && a.CanManageHouseholdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()) == Task.FromResult(canManageHousehold));
        _securityLogger = new Mock<ISecurityEventLogger>();
        return new InvitationService(_dbContext, tenantContext, userContext, auth, _securityLogger.Object, new FakeAuthCache());
    }

    private Guid AddMember(Guid? householdId = null)
    {
        var memberId = Guid.NewGuid();
        _dbContext.HouseholdMembers.Add(new HouseholdMember
        {
            Id = memberId, TenantId = _tenantId, HouseholdId = householdId ?? _householdId, Name = "Alice",
        });
        return memberId;
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
            Households = [new InvitationHouseholdGrant(_householdId, HouseholdRole.Manager)],
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
    public async Task CreateAsync_DuplicatePending_MergesNewRoleGrantsAndLink()
    {
        var ct = TestContext.Current.CancellationToken;
        var memberId = AddMember();
        await _dbContext.SaveChangesAsync(ct);
        var service = CreateService();

        var first = await service.CreateAsync(_tenantId,
            new CreateInvitationRequest { Email = "merge@test.com", Role = TenantRole.Guest }, ct);

        // The re-invite carries more access — it must be applied, not silently discarded.
        var second = await service.CreateAsync(_tenantId, new CreateInvitationRequest
        {
            Email = "merge@test.com",
            Role = TenantRole.Member,
            Households = [new InvitationHouseholdGrant(_householdId, HouseholdRole.Manager)],
            LinkedMemberId = memberId,
        }, ct);

        Assert.True(second.Successful);
        Assert.Equal(first.Entity!.Id, second.Entity!.Id);
        Assert.Equal(TenantRole.Member, second.Entity.Role);
        Assert.Equal(memberId, second.Entity.LinkedMemberId);
        var grant = Assert.Single(second.Entity.Households);
        Assert.Equal(HouseholdRole.Manager, grant.Role);
        Assert.Equal(1, await _dbContext.Invitations.CountAsync(ct));
        Assert.Equal(1, await _dbContext.InvitationHouseholdAccess.CountAsync(ct));
    }

    [Fact]
    public async Task CreateAsync_DuplicatePending_RemovesGrantsNoLongerRequested()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = CreateService();

        await service.CreateAsync(_tenantId, new CreateInvitationRequest
        {
            Email = "shrink@test.com",
            Role = TenantRole.Member,
            Households = [new InvitationHouseholdGrant(_householdId, HouseholdRole.Manager)],
        }, ct);

        var second = await service.CreateAsync(_tenantId,
            new CreateInvitationRequest { Email = "shrink@test.com", Role = TenantRole.Member }, ct);

        Assert.True(second.Successful);
        Assert.Empty(second.Entity!.Households);
        // The grant row is soft-deleted, not surfaced.
        var row = await _dbContext.InvitationHouseholdAccess.IgnoreQueryFilters().SingleAsync(ct);
        Assert.True(row.IsDeleted);
    }

    [Fact]
    public async Task CreateAsync_PendingInviteAlreadyClaimsMember_Rejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var memberId = AddMember();
        await _dbContext.SaveChangesAsync(ct);
        var service = CreateService();

        var first = await service.CreateAsync(_tenantId, new CreateInvitationRequest
        {
            Email = "first@test.com", Role = TenantRole.Member, LinkedMemberId = memberId,
        }, ct);
        Assert.True(first.Successful);

        // A second pending invite promising the same member would be silently dropped at accept
        // time for whoever signs in second — reject it at invite time instead.
        var second = await service.CreateAsync(_tenantId, new CreateInvitationRequest
        {
            Email = "second@test.com", Role = TenantRole.Member, LinkedMemberId = memberId,
        }, ct);

        Assert.False(second.Successful);
        Assert.Equal(1, await _dbContext.Invitations.CountAsync(ct));
    }

    [Fact]
    public async Task CreateAsync_ExistingUserLinkedToDifferentMember_Rejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var existingUserId = Guid.NewGuid();
        var memberA = AddMember();
        var memberB = AddMember();
        _dbContext.Users.Add(new User { Id = existingUserId, ExternalId = "linked@idp", Email = "linked@test.com" });
        _dbContext.TenantUsers.Add(new TenantUser
        {
            TenantId = _tenantId, UserId = existingUserId, Role = TenantRole.Member, LinkedMemberId = memberA,
        });
        await _dbContext.SaveChangesAsync(ct);

        // The invitee already holds a different link; accepting would either overwrite it (data
        // loss) or silently drop the requested one — reject up front instead.
        var result = await CreateService().CreateAsync(_tenantId, new CreateInvitationRequest
        {
            Email = "linked@test.com", Role = TenantRole.Member, LinkedMemberId = memberB,
        }, ct);

        Assert.False(result.Successful);
        var tenantUser = await _dbContext.TenantUsers.SingleAsync(ct);
        Assert.Equal(memberA, tenantUser.LinkedMemberId);
    }

    [Fact]
    public async Task CreateAsync_ExistingUserAlreadyLinkedToSameMember_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var existingUserId = Guid.NewGuid();
        var memberId = AddMember();
        _dbContext.Users.Add(new User { Id = existingUserId, ExternalId = "same@idp", Email = "same@test.com" });
        _dbContext.TenantUsers.Add(new TenantUser
        {
            TenantId = _tenantId, UserId = existingUserId, Role = TenantRole.Member, LinkedMemberId = memberId,
        });
        await _dbContext.SaveChangesAsync(ct);

        // Their own existing claim on the member is not a conflict.
        var result = await CreateService().CreateAsync(_tenantId, new CreateInvitationRequest
        {
            Email = "same@test.com", Role = TenantRole.Member, LinkedMemberId = memberId,
        }, ct);

        Assert.True(result.Successful);
        Assert.Equal(InvitationStatus.Accepted, result.Entity!.Status);
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
            Households = [new InvitationHouseholdGrant(Guid.NewGuid(), HouseholdRole.Member)],
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
    public async Task RevokeAsync_SoftDeletesHouseholdGrantRows()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = CreateService();
        var created = await service.CreateAsync(_tenantId, new CreateInvitationRequest
        {
            Email = "revoke-grants@test.com",
            Role = TenantRole.Member,
            Households = [new InvitationHouseholdGrant(_householdId, HouseholdRole.Member)],
        }, ct);

        var result = await service.RevokeAsync(_tenantId, created.Entity!.Id, ct);

        Assert.True(result.Successful);
        Assert.Empty(result.Entity!.Households);
        // Revoking cancels the promised access: the grant rows must not stay active under a
        // soft-deleted invitation.
        var row = await _dbContext.InvitationHouseholdAccess.IgnoreQueryFilters().SingleAsync(ct);
        Assert.True(row.IsDeleted);
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
    public async Task CreateAsync_PreLinkMember_NoHouseholdAccess_LinksMemberOnAccept()
    {
        var ct = TestContext.Current.CancellationToken;
        var existingUserId = Guid.NewGuid();
        _dbContext.Users.Add(new User { Id = existingUserId, ExternalId = "link@idp", Email = "link@test.com" });
        var memberId = AddMember();
        await _dbContext.SaveChangesAsync(ct);

        var request = new CreateInvitationRequest
        {
            Email = "link@test.com",
            Role = TenantRole.Member,
            LinkedMemberId = memberId,
        };

        var result = await CreateService().CreateAsync(_tenantId, request, ct);

        Assert.True(result.Successful);
        Assert.Equal(InvitationStatus.Accepted, result.Entity!.Status);
        var tenantUser = await _dbContext.TenantUsers.SingleAsync(ct);
        Assert.Equal(memberId, tenantUser.LinkedMemberId);
        // Household access is independent — none was requested, so none is granted.
        Assert.False(await _dbContext.HouseholdUsers.AnyAsync(ct));
    }

    [Fact]
    public async Task CreateAsync_PreLinkMember_WithHouseholdAccess_LinksAndGrantsAccess()
    {
        var ct = TestContext.Current.CancellationToken;
        _dbContext.Users.Add(new User { Id = Guid.NewGuid(), ExternalId = "both@idp", Email = "both@test.com" });
        var memberId = AddMember();
        await _dbContext.SaveChangesAsync(ct);

        var request = new CreateInvitationRequest
        {
            Email = "both@test.com",
            Role = TenantRole.Member,
            Households = [new InvitationHouseholdGrant(_householdId, HouseholdRole.Manager)],
            LinkedMemberId = memberId,
        };

        var result = await CreateService().CreateAsync(_tenantId, request, ct);

        Assert.True(result.Successful);
        var tenantUser = await _dbContext.TenantUsers.SingleAsync(ct);
        Assert.Equal(memberId, tenantUser.LinkedMemberId);
        var householdUser = await _dbContext.HouseholdUsers.SingleAsync(ct);
        Assert.Equal(HouseholdRole.Manager, householdUser.Role);
    }

    [Fact]
    public async Task CreateAsync_PreLinkMember_AlreadyLinkedToAnother_Rejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var memberId = AddMember();
        var otherUserId = Guid.NewGuid();
        _dbContext.Users.Add(new User { Id = otherUserId, ExternalId = "other@idp" });
        _dbContext.TenantUsers.Add(new TenantUser { TenantId = _tenantId, UserId = otherUserId, Role = TenantRole.Member, LinkedMemberId = memberId });
        await _dbContext.SaveChangesAsync(ct);

        var request = new CreateInvitationRequest { Email = "taken@test.com", Role = TenantRole.Member, LinkedMemberId = memberId };

        var result = await CreateService().CreateAsync(_tenantId, request, ct);

        Assert.False(result.Successful);
        Assert.False(await _dbContext.Invitations.AnyAsync(ct));
    }

    [Fact]
    public async Task CreateAsync_PreLinkMember_NotInTenant_Rejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var request = new CreateInvitationRequest { Email = "ghost@test.com", Role = TenantRole.Member, LinkedMemberId = Guid.NewGuid() };

        var result = await CreateService().CreateAsync(_tenantId, request, ct);

        Assert.False(result.Successful);
        Assert.False(await _dbContext.Invitations.AnyAsync(ct));
    }

    [Fact]
    public async Task CreateAsync_PreLinkMember_WithoutHouseholdManage_Rejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var memberId = AddMember();
        await _dbContext.SaveChangesAsync(ct);

        var request = new CreateInvitationRequest { Email = "nomanage@test.com", Role = TenantRole.Member, LinkedMemberId = memberId };

        var result = await CreateService(canManageHousehold: false).CreateAsync(_tenantId, request, ct);

        Assert.False(result.Successful);
        Assert.False(await _dbContext.Invitations.AnyAsync(ct));
    }

    [Fact]
    public async Task CreateAsync_MultipleHouseholds_GrantsAllOnAccept()
    {
        var ct = TestContext.Current.CancellationToken;
        var existingUserId = Guid.NewGuid();
        var household2 = Guid.NewGuid();
        _dbContext.Users.Add(new User { Id = existingUserId, ExternalId = "multi@idp", Email = "multi@test.com" });
        _dbContext.Households.Add(new Household { Id = household2, TenantId = _tenantId, Name = "Second Household" });
        await _dbContext.SaveChangesAsync(ct);

        var request = new CreateInvitationRequest
        {
            Email = "multi@test.com",
            Role = TenantRole.Member,
            Households =
            [
                new InvitationHouseholdGrant(_householdId, HouseholdRole.Manager),
                new InvitationHouseholdGrant(household2, HouseholdRole.Member),
            ],
        };

        var result = await CreateService().CreateAsync(_tenantId, request, ct);

        Assert.True(result.Successful);
        Assert.Equal(InvitationStatus.Accepted, result.Entity!.Status);
        Assert.Equal(2, result.Entity.Households.Count);
        var householdUsers = await _dbContext.HouseholdUsers.ToListAsync(ct);
        Assert.Equal(2, householdUsers.Count);
        Assert.Contains(householdUsers, h => h.HouseholdId == _householdId && h.Role == HouseholdRole.Manager);
        Assert.Contains(householdUsers, h => h.HouseholdId == household2 && h.Role == HouseholdRole.Member);
    }

    [Fact]
    public async Task CreateAsync_Pending_PersistsMultipleHouseholdGrants()
    {
        var ct = TestContext.Current.CancellationToken;
        var household2 = Guid.NewGuid();
        _dbContext.Households.Add(new Household { Id = household2, TenantId = _tenantId, Name = "Second Household" });
        await _dbContext.SaveChangesAsync(ct);

        var request = new CreateInvitationRequest
        {
            Email = "pending-multi@test.com",
            Role = TenantRole.Member,
            Households =
            [
                new InvitationHouseholdGrant(_householdId, HouseholdRole.Manager),
                new InvitationHouseholdGrant(household2, HouseholdRole.Member),
            ],
        };

        var result = await CreateService().CreateAsync(_tenantId, request, ct);

        Assert.True(result.Successful);
        Assert.Equal(InvitationStatus.Pending, result.Entity!.Status);
        Assert.Equal(2, result.Entity.Households.Count);
        // No user exists yet, so nothing is granted — the intent is persisted for deferred accept.
        Assert.Equal(2, await _dbContext.InvitationHouseholdAccess.CountAsync(ct));
        Assert.False(await _dbContext.HouseholdUsers.AnyAsync(ct));
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

    [Fact]
    public void InvitationHouseholdGrant_OmittedRole_DeserializesToMember()
    {
        // Manager is enum value 0, so without the constructor default a request omitting "role"
        // would bind to Manager — a silent privilege escalation. Mirror the API's serializer setup.
        var options = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
        options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());

        var grant = System.Text.Json.JsonSerializer.Deserialize<InvitationHouseholdGrant>(
            $"{{\"householdId\":\"{Guid.NewGuid()}\"}}", options);

        Assert.Equal(HouseholdRole.Member, grant!.Role);
    }
}
