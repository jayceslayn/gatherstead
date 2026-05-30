using Gatherstead.Api.Security;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.Observability;
using Gatherstead.Api.Tests.Fixtures;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;

namespace Gatherstead.Api.Tests.Services;

public class MemberAuthorizationServiceTests : IAsyncLifetime
{
    private GathersteadDbContext _dbContext = null!;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _householdId = Guid.NewGuid();

    public async ValueTask InitializeAsync()
    {
        _dbContext = TestDbContextFactory.Create(tenantId: _tenantId, currentUserId: _userId);
        _dbContext.Tenants.Add(new Tenant { Id = _tenantId, Name = "Test Tenant" });
        _dbContext.Users.Add(new User { Id = _userId, ExternalId = _userId.ToString() });
        await _dbContext.SaveChangesAsync();
    }

    public ValueTask DisposeAsync()
    {
        _dbContext.Dispose();
        return ValueTask.CompletedTask;
    }

    private MemberAuthorizationService CreateService(Guid? userId, bool isAppAdmin = false)
    {
        var userContext = Mock.Of<ICurrentUserContext>(c => c.UserId == userId);
        var appAdminContext = Mock.Of<IAppAdminContext>(c =>
            c.IsAppAdminAsync(It.IsAny<CancellationToken>()) == Task.FromResult<bool?>(isAppAdmin ? true : (bool?)false));
        var httpContextAccessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        var securityLogger = new Mock<ISecurityEventLogger>();
        securityLogger
            .Setup(s => s.LogAsync(
                It.IsAny<SecurityEventType>(), It.IsAny<SecurityEventSeverity>(),
                It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new MemberAuthorizationService(
            _dbContext,
            userContext,
            httpContextAccessor,
            appAdminContext,
            Mock.Of<ILogger<MemberAuthorizationService>>(),
            securityLogger.Object);
    }

    private async Task SeedTenantUserAsync(TenantRole role)
    {
        _dbContext.TenantUsers.Add(new TenantUser { TenantId = _tenantId, UserId = _userId, Role = role });
        await _dbContext.SaveChangesAsync();
    }

    private async Task SeedHouseholdMemberAsync(Guid memberId, Guid householdId)
    {
        if (!_dbContext.Households.Local.Any(h => h.Id == householdId))
            _dbContext.Households.Add(new Household { Id = householdId, TenantId = _tenantId, Name = $"Household {householdId}" });

        _dbContext.HouseholdMembers.Add(new HouseholdMember
        {
            Id = memberId,
            TenantId = _tenantId,
            HouseholdId = householdId,
            Name = "Test Member",
        });
        await _dbContext.SaveChangesAsync();
    }

    private async Task SeedLinkedMemberAsync(Guid memberId)
    {
        var tenantUser = await _dbContext.TenantUsers.FindAsync(_tenantId, _userId)
            ?? throw new InvalidOperationException("TenantUser must be seeded before calling SeedLinkedMemberAsync.");
        tenantUser.LinkedMemberId = memberId;
        await _dbContext.SaveChangesAsync();
    }

    private async Task SeedHouseholdUserAsync(Guid householdId, HouseholdRole role)
    {
        if (!_dbContext.Households.Local.Any(h => h.Id == householdId))
            _dbContext.Households.Add(new Household { Id = householdId, TenantId = _tenantId, Name = $"Household {householdId}" });

        _dbContext.HouseholdUsers.Add(new HouseholdUser
        {
            TenantId = _tenantId,
            HouseholdId = householdId,
            UserId = _userId,
            Role = role,
        });
        await _dbContext.SaveChangesAsync();
    }

    // ── CanManageTenantAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task CanManageTenantAsync_Unauthenticated_ReturnsFalse()
    {
        var service = CreateService(userId: null);
        Assert.False(await service.CanManageTenantAsync(_tenantId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CanManageTenantAsync_AppAdmin_ReturnsTrue()
    {
        var service = CreateService(_userId, isAppAdmin: true);
        Assert.True(await service.CanManageTenantAsync(_tenantId, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(TenantRole.Owner)]
    [InlineData(TenantRole.Manager)]
    public async Task CanManageTenantAsync_OwnerOrManager_ReturnsTrue(TenantRole role)
    {
        await SeedTenantUserAsync(role);
        var service = CreateService(_userId);
        Assert.True(await service.CanManageTenantAsync(_tenantId, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(TenantRole.Coordinator)]
    [InlineData(TenantRole.Member)]
    [InlineData(TenantRole.Guest)]
    public async Task CanManageTenantAsync_CoordinatorOrBelow_ReturnsFalse(TenantRole role)
    {
        await SeedTenantUserAsync(role);
        var service = CreateService(_userId);
        Assert.False(await service.CanManageTenantAsync(_tenantId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CanManageTenantAsync_NonTenantMember_ReturnsFalse()
    {
        var service = CreateService(_userId);
        Assert.False(await service.CanManageTenantAsync(_tenantId, TestContext.Current.CancellationToken));
    }

    // ── CanManageEventAsync ──────────────────────────────────────────────────

    [Theory]
    [InlineData(TenantRole.Owner)]
    [InlineData(TenantRole.Manager)]
    [InlineData(TenantRole.Coordinator)]
    public async Task CanManageEventAsync_CoordinatorOrAbove_ReturnsTrue(TenantRole role)
    {
        await SeedTenantUserAsync(role);
        var service = CreateService(_userId);
        Assert.True(await service.CanManageEventAsync(_tenantId, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(TenantRole.Member)]
    [InlineData(TenantRole.Guest)]
    public async Task CanManageEventAsync_MemberOrGuest_ReturnsFalse(TenantRole role)
    {
        await SeedTenantUserAsync(role);
        var service = CreateService(_userId);
        Assert.False(await service.CanManageEventAsync(_tenantId, TestContext.Current.CancellationToken));
    }

    // ── CanEditMemberAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task CanEditMemberAsync_Unauthenticated_ReturnsFalse()
    {
        var service = CreateService(userId: null);
        Assert.False(await service.CanEditMemberAsync(_tenantId, _householdId, Guid.NewGuid(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CanEditMemberAsync_AppAdmin_ReturnsTrue()
    {
        var service = CreateService(_userId, isAppAdmin: true);
        Assert.True(await service.CanEditMemberAsync(_tenantId, _householdId, Guid.NewGuid(), TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(TenantRole.Owner)]
    [InlineData(TenantRole.Manager)]
    public async Task CanEditMemberAsync_OwnerOrManager_ReturnsTrue(TenantRole role)
    {
        await SeedTenantUserAsync(role);
        var service = CreateService(_userId);
        Assert.True(await service.CanEditMemberAsync(_tenantId, _householdId, Guid.NewGuid(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CanEditMemberAsync_Self_ReturnsTrue()
    {
        var memberId = Guid.NewGuid();
        await SeedTenantUserAsync(TenantRole.Member);
        await SeedHouseholdMemberAsync(memberId, _householdId);
        await SeedLinkedMemberAsync(memberId);

        var service = CreateService(_userId);
        Assert.True(await service.CanEditMemberAsync(_tenantId, _householdId, memberId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CanEditMemberAsync_HouseholdManager_CanEditMemberInSameHousehold()
    {
        await SeedTenantUserAsync(TenantRole.Member);
        await SeedHouseholdUserAsync(_householdId, HouseholdRole.Manager);

        var service = CreateService(_userId);
        Assert.True(await service.CanEditMemberAsync(_tenantId, _householdId, Guid.NewGuid(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CanEditMemberAsync_HouseholdManager_CannotEditMemberInDifferentHousehold()
    {
        var otherHouseholdId = Guid.NewGuid();
        await SeedTenantUserAsync(TenantRole.Member);
        await SeedHouseholdUserAsync(_householdId, HouseholdRole.Manager);

        var service = CreateService(_userId);
        Assert.False(await service.CanEditMemberAsync(_tenantId, otherHouseholdId, Guid.NewGuid(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CanEditMemberAsync_MemberWithNoLinkedRecord_ReturnsFalse()
    {
        await SeedTenantUserAsync(TenantRole.Member);
        var service = CreateService(_userId);
        Assert.False(await service.CanEditMemberAsync(_tenantId, _householdId, Guid.NewGuid(), TestContext.Current.CancellationToken));
    }

    // ── CanManageHouseholdAsync ──────────────────────────────────────────────

    [Fact]
    public async Task CanManageHouseholdAsync_Unauthenticated_ReturnsFalse()
    {
        var service = CreateService(userId: null);
        Assert.False(await service.CanManageHouseholdAsync(_tenantId, _householdId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CanManageHouseholdAsync_AppAdmin_ReturnsTrue()
    {
        var service = CreateService(_userId, isAppAdmin: true);
        Assert.True(await service.CanManageHouseholdAsync(_tenantId, _householdId, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(TenantRole.Owner)]
    [InlineData(TenantRole.Manager)]
    public async Task CanManageHouseholdAsync_OwnerOrManager_ReturnsTrue(TenantRole role)
    {
        await SeedTenantUserAsync(role);
        var service = CreateService(_userId);
        Assert.True(await service.CanManageHouseholdAsync(_tenantId, _householdId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CanManageHouseholdAsync_HouseholdManager_ReturnsTrue()
    {
        await SeedTenantUserAsync(TenantRole.Member);
        await SeedHouseholdUserAsync(_householdId, HouseholdRole.Manager);

        var service = CreateService(_userId);
        Assert.True(await service.CanManageHouseholdAsync(_tenantId, _householdId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CanManageHouseholdAsync_HouseholdMember_ReturnsFalse()
    {
        await SeedTenantUserAsync(TenantRole.Member);
        await SeedHouseholdUserAsync(_householdId, HouseholdRole.Member);

        var service = CreateService(_userId);
        Assert.False(await service.CanManageHouseholdAsync(_tenantId, _householdId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CanManageHouseholdAsync_ManagerInDifferentHousehold_ReturnsFalse()
    {
        var otherHouseholdId = Guid.NewGuid();
        await SeedTenantUserAsync(TenantRole.Member);
        await SeedHouseholdUserAsync(otherHouseholdId, HouseholdRole.Manager);

        var service = CreateService(_userId);
        Assert.False(await service.CanManageHouseholdAsync(_tenantId, _householdId, TestContext.Current.CancellationToken));
    }

    // ── GetSensitiveReadScopeAsync ───────────────────────────────────────────

    [Fact]
    public async Task GetSensitiveReadScopeAsync_AppAdmin_ReturnsNone()
    {
        // App Admins have write authority but PII is deliberately redacted from read paths.
        var service = CreateService(_userId, isAppAdmin: true);
        var scope = await service.GetSensitiveReadScopeAsync(_tenantId, TestContext.Current.CancellationToken);
        Assert.False(scope.IsGlobal);
        Assert.False(scope.CanReadSensitive(_householdId));
    }

    [Theory]
    [InlineData(TenantRole.Owner)]
    [InlineData(TenantRole.Manager)]
    [InlineData(TenantRole.Coordinator)]
    [InlineData(TenantRole.Member)]
    public async Task GetSensitiveReadScopeAsync_TenantMemberOrAbove_ReturnsGlobal(TenantRole role)
    {
        await SeedTenantUserAsync(role);
        var service = CreateService(_userId);
        var scope = await service.GetSensitiveReadScopeAsync(_tenantId, TestContext.Current.CancellationToken);
        Assert.True(scope.IsGlobal);
        Assert.True(scope.CanReadSensitive(_householdId));
        Assert.True(scope.CanReadSensitive(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetSensitiveReadScopeAsync_GuestWithHouseholdManagerRole_ReturnsForHouseholds()
    {
        await SeedTenantUserAsync(TenantRole.Guest);
        await SeedHouseholdUserAsync(_householdId, HouseholdRole.Manager);

        var service = CreateService(_userId);
        var scope = await service.GetSensitiveReadScopeAsync(_tenantId, TestContext.Current.CancellationToken);
        Assert.False(scope.IsGlobal);
        Assert.True(scope.CanReadSensitive(_householdId));
        Assert.False(scope.CanReadSensitive(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetSensitiveReadScopeAsync_GuestWithHouseholdMemberRole_ReturnsForHouseholds()
    {
        await SeedTenantUserAsync(TenantRole.Guest);
        await SeedHouseholdUserAsync(_householdId, HouseholdRole.Member);

        var service = CreateService(_userId);
        var scope = await service.GetSensitiveReadScopeAsync(_tenantId, TestContext.Current.CancellationToken);
        Assert.False(scope.IsGlobal);
        Assert.True(scope.CanReadSensitive(_householdId));
        Assert.False(scope.CanReadSensitive(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetSensitiveReadScopeAsync_GuestWithNoHouseholdUser_ReturnsNone()
    {
        await SeedTenantUserAsync(TenantRole.Guest);

        var service = CreateService(_userId);
        var scope = await service.GetSensitiveReadScopeAsync(_tenantId, TestContext.Current.CancellationToken);
        Assert.False(scope.IsGlobal);
        Assert.False(scope.CanReadSensitive(_householdId));
        Assert.False(scope.CanReadSensitive(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetSensitiveReadScopeAsync_Unauthenticated_ReturnsNone()
    {
        var service = CreateService(userId: null);
        var scope = await service.GetSensitiveReadScopeAsync(_tenantId, TestContext.Current.CancellationToken);
        Assert.False(scope.IsGlobal);
        Assert.False(scope.CanReadSensitive(_householdId));
    }

    // ── GetCallerTenantRoleAsync ─────────────────────────────────────────────

    [Fact]
    public async Task GetCallerTenantRoleAsync_Unauthenticated_ReturnsNull()
    {
        var service = CreateService(userId: null);
        Assert.Null(await service.GetCallerTenantRoleAsync(_tenantId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetCallerTenantRoleAsync_AppAdmin_ReturnsNull()
    {
        // App Admins are not tenant members; attribute visibility treats them as having no role.
        var service = CreateService(_userId, isAppAdmin: true);
        Assert.Null(await service.GetCallerTenantRoleAsync(_tenantId, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(TenantRole.Owner)]
    [InlineData(TenantRole.Manager)]
    [InlineData(TenantRole.Coordinator)]
    [InlineData(TenantRole.Member)]
    [InlineData(TenantRole.Guest)]
    public async Task GetCallerTenantRoleAsync_TenantMember_ReturnsRole(TenantRole role)
    {
        await SeedTenantUserAsync(role);
        var service = CreateService(_userId);
        Assert.Equal(role, await service.GetCallerTenantRoleAsync(_tenantId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetCallerTenantRoleAsync_NonMember_ReturnsNull()
    {
        var service = CreateService(_userId);
        Assert.Null(await service.GetCallerTenantRoleAsync(_tenantId, TestContext.Current.CancellationToken));
    }

    // ── GetCallerHouseholdRoleAsync ──────────────────────────────────────────

    [Fact]
    public async Task GetCallerHouseholdRoleAsync_Unauthenticated_ReturnsNull()
    {
        var service = CreateService(userId: null);
        Assert.Null(await service.GetCallerHouseholdRoleAsync(_tenantId, _householdId, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(HouseholdRole.Manager)]
    [InlineData(HouseholdRole.Member)]
    public async Task GetCallerHouseholdRoleAsync_MemberOfHousehold_ReturnsRole(HouseholdRole role)
    {
        await SeedTenantUserAsync(TenantRole.Member);
        await SeedHouseholdUserAsync(_householdId, role);
        var service = CreateService(_userId);
        Assert.Equal(role, await service.GetCallerHouseholdRoleAsync(_tenantId, _householdId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetCallerHouseholdRoleAsync_MemberOfDifferentHousehold_ReturnsNull()
    {
        var otherHouseholdId = Guid.NewGuid();
        await SeedTenantUserAsync(TenantRole.Member);
        await SeedHouseholdUserAsync(otherHouseholdId, HouseholdRole.Manager);
        var service = CreateService(_userId);
        Assert.Null(await service.GetCallerHouseholdRoleAsync(_tenantId, _householdId, TestContext.Current.CancellationToken));
    }
}
