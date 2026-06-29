using System.Security.Claims;
using Gatherstead.Api.Services.Observability;
using Gatherstead.Api.Services.Provisioning;
using Gatherstead.Api.Tests.Fixtures;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Gatherstead.Api.Tests.Services;

public class UserProvisioningServiceTests : IAsyncLifetime
{
    private GathersteadDbContext _dbContext = null!;
    private Mock<ISecurityEventLogger> _securityLogger = null!;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _householdId = Guid.NewGuid();
    private const string ExternalId = "sub-12345";
    private const string Email = "invitee@test.com";

    public async ValueTask InitializeAsync()
    {
        // No ambient tenant context: bootstrap runs before a tenant is resolved.
        _dbContext = TestDbContextFactory.Create();
        _dbContext.Tenants.Add(new Tenant { Id = _tenantId, Name = "Test Tenant" });
        _dbContext.Households.Add(new Household { Id = _householdId, TenantId = _tenantId, Name = "Smith Household" });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        _dbContext.Dispose();
        return ValueTask.CompletedTask;
    }

    private static IHttpContextAccessor BuildAccessor(
        string? externalId = ExternalId,
        string? email = Email,
        bool emailVerified = true,
        bool authenticated = true)
    {
        var claims = new List<Claim>();
        if (externalId is not null) claims.Add(new Claim("sub", externalId));
        if (email is not null) claims.Add(new Claim("email", email));
        claims.Add(new Claim("email_verified", emailVerified ? "true" : "false"));

        var identity = authenticated ? new ClaimsIdentity(claims, "TestAuth") : new ClaimsIdentity();
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        return new HttpContextAccessor { HttpContext = httpContext };
    }

    private UserProvisioningService CreateService(IHttpContextAccessor accessor)
    {
        _securityLogger = new Mock<ISecurityEventLogger>();
        return new(_dbContext, accessor, _securityLogger.Object);
    }

    private void SeedPendingInvitation(string email, TenantRole role = TenantRole.Member, Guid? householdId = null, HouseholdRole? householdRole = null, Guid? invitedByUserId = null)
    {
        _dbContext.Invitations.Add(new Invitation
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            Email = email,
            Role = role,
            HouseholdId = householdId,
            HouseholdRole = householdRole,
            Status = InvitationStatus.Pending,
            InvitedByUserId = invitedByUserId,
        });
        _dbContext.SaveChanges();
    }

    [Fact]
    public async Task BootstrapAsync_Unauthenticated_ReturnsError()
    {
        var result = await CreateService(BuildAccessor(authenticated: false))
            .BootstrapAsync(TestContext.Current.CancellationToken);

        Assert.False(result.Successful);
    }

    [Fact]
    public async Task BootstrapAsync_NewUser_CreatesUserRow()
    {
        var result = await CreateService(BuildAccessor()).BootstrapAsync(TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        var user = await _dbContext.Users.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(ExternalId, user.ExternalId);
        Assert.Equal(Email, user.Email);
        Assert.Equal(user.Id, result.Entity!.UserId);
    }

    [Fact]
    public async Task BootstrapAsync_ExistingUser_RefreshesEmail()
    {
        var userId = Guid.NewGuid();
        _dbContext.Users.Add(new User { Id = userId, ExternalId = ExternalId, Email = "stale@test.com" });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await CreateService(BuildAccessor()).BootstrapAsync(TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        Assert.Equal(userId, result.Entity!.UserId);
        var user = await _dbContext.Users.SingleAsync(u => u.Id == userId, TestContext.Current.CancellationToken);
        Assert.Equal(Email, user.Email);
    }

    [Fact]
    public async Task BootstrapAsync_VerifiedEmail_ClaimsPendingInvitation()
    {
        SeedPendingInvitation(Email, TenantRole.Coordinator, _householdId, HouseholdRole.Manager);

        var result = await CreateService(BuildAccessor()).BootstrapAsync(TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        Assert.Equal(1, result.Entity!.ClaimedInvitations);
        Assert.Contains(result.Entity.Tenants, t => t.TenantId == _tenantId && t.Role == TenantRole.Coordinator);

        var tenantUser = await _dbContext.TenantUsers.IgnoreQueryFilters().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(TenantRole.Coordinator, tenantUser.Role);

        var householdUser = await _dbContext.HouseholdUsers.IgnoreQueryFilters().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(HouseholdRole.Manager, householdUser.Role);

        var invite = await _dbContext.Invitations.IgnoreQueryFilters().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(InvitationStatus.Accepted, invite.Status);
        Assert.Equal(result.Entity.UserId, invite.AcceptedByUserId);
    }

    [Fact]
    public async Task BootstrapAsync_UnverifiedEmail_DoesNotClaimInvitation()
    {
        SeedPendingInvitation(Email);

        var result = await CreateService(BuildAccessor(emailVerified: false))
            .BootstrapAsync(TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        Assert.Equal(0, result.Entity!.ClaimedInvitations);
        Assert.False(await _dbContext.TenantUsers.IgnoreQueryFilters().AnyAsync(TestContext.Current.CancellationToken));

        var invite = await _dbContext.Invitations.IgnoreQueryFilters().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(InvitationStatus.Pending, invite.Status);
    }

    [Fact]
    public async Task BootstrapAsync_EmailCaseInsensitiveMatch_ClaimsInvitation()
    {
        SeedPendingInvitation("invitee@test.com");

        var result = await CreateService(BuildAccessor(email: "INVITEE@TEST.COM"))
            .BootstrapAsync(TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        Assert.Equal(1, result.Entity!.ClaimedInvitations);
    }

    [Fact]
    public async Task BootstrapAsync_RerunIsIdempotent()
    {
        SeedPendingInvitation(Email);
        var service = CreateService(BuildAccessor());

        var first = await service.BootstrapAsync(TestContext.Current.CancellationToken);
        var second = await service.BootstrapAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, first.Entity!.ClaimedInvitations);
        Assert.Equal(0, second.Entity!.ClaimedInvitations);
        Assert.Equal(1, await _dbContext.TenantUsers.IgnoreQueryFilters().CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task BootstrapAsync_NewUser_IsAppAdminFalse()
    {
        var result = await CreateService(BuildAccessor()).BootstrapAsync(TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        Assert.False(result.Entity!.IsAppAdmin);
    }

    [Fact]
    public async Task BootstrapAsync_AppAdminUser_SurfacesIsAppAdmin()
    {
        _dbContext.Users.Add(new User { Id = Guid.NewGuid(), ExternalId = ExternalId, Email = Email, IsAppAdmin = true });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await CreateService(BuildAccessor()).BootstrapAsync(TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        Assert.True(result.Entity!.IsAppAdmin);
    }

    [Fact]
    public async Task BootstrapAsync_ClaimInvitation_LogsAcceptedAsInfo()
    {
        SeedPendingInvitation(Email, invitedByUserId: Guid.NewGuid());

        var service = CreateService(BuildAccessor());
        await service.BootstrapAsync(TestContext.Current.CancellationToken);

        // An ordinary accept (inviter != acceptor) is informational.
        _securityLogger.Verify(s => s.LogAsync(
            SecurityEventType.InvitationAccepted,
            SecurityEventSeverity.Info,
            It.IsAny<string>(), It.IsAny<string>(), _tenantId, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task BootstrapAsync_SelfGrantInvitation_LogsAcceptedAsWarning()
    {
        // The user who created the invite is the same account that claims it — the self-grant abuse path.
        var userId = Guid.NewGuid();
        _dbContext.Users.Add(new User { Id = userId, ExternalId = ExternalId, Email = Email });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        SeedPendingInvitation(Email, role: TenantRole.Owner, invitedByUserId: userId);

        var service = CreateService(BuildAccessor());
        await service.BootstrapAsync(TestContext.Current.CancellationToken);

        _securityLogger.Verify(s => s.LogAsync(
            SecurityEventType.InvitationAccepted,
            SecurityEventSeverity.Warning,
            It.IsAny<string>(), It.IsAny<string>(), _tenantId, userId, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
