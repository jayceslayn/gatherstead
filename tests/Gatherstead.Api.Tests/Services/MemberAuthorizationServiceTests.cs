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

    private async Task SeedHouseholdMemberAsync(Guid memberId, Guid householdId, HouseholdRole householdRole)
    {
        if (!_dbContext.Households.Local.Any(h => h.Id == householdId))
            _dbContext.Households.Add(new Household { Id = householdId, TenantId = _tenantId, Name = $"Household {householdId}" });

        _dbContext.HouseholdMembers.Add(new HouseholdMember
        {
            Id = memberId,
            TenantId = _tenantId,
            HouseholdId = householdId,
            UserId = _userId,
            Name = "Test Member",
            HouseholdRole = householdRole,
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
    [InlineData(TenantRole.Member)]
    [InlineData(TenantRole.Guest)]
    public async Task CanManageTenantAsync_MemberOrGuest_ReturnsFalse(TenantRole role)
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
        await SeedHouseholdMemberAsync(memberId, _householdId, HouseholdRole.Member);

        var service = CreateService(_userId);
        Assert.True(await service.CanEditMemberAsync(_tenantId, _householdId, memberId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CanEditMemberAsync_HouseholdAdmin_CanEditMemberInSameHousehold()
    {
        var adminMemberId = Guid.NewGuid();
        await SeedTenantUserAsync(TenantRole.Member);
        await SeedHouseholdMemberAsync(adminMemberId, _householdId, HouseholdRole.Admin);

        var service = CreateService(_userId);
        Assert.True(await service.CanEditMemberAsync(_tenantId, _householdId, Guid.NewGuid(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CanEditMemberAsync_HouseholdAdmin_CannotEditMemberInDifferentHousehold()
    {
        var adminMemberId = Guid.NewGuid();
        var otherHouseholdId = Guid.NewGuid();
        await SeedTenantUserAsync(TenantRole.Member);
        await SeedHouseholdMemberAsync(adminMemberId, _householdId, HouseholdRole.Admin);

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
    public async Task CanManageHouseholdAsync_HouseholdAdmin_ReturnsTrue()
    {
        await SeedTenantUserAsync(TenantRole.Member);
        await SeedHouseholdMemberAsync(Guid.NewGuid(), _householdId, HouseholdRole.Admin);

        var service = CreateService(_userId);
        Assert.True(await service.CanManageHouseholdAsync(_tenantId, _householdId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CanManageHouseholdAsync_RegularMember_ReturnsFalse()
    {
        await SeedTenantUserAsync(TenantRole.Member);
        await SeedHouseholdMemberAsync(Guid.NewGuid(), _householdId, HouseholdRole.Member);

        var service = CreateService(_userId);
        Assert.False(await service.CanManageHouseholdAsync(_tenantId, _householdId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CanManageHouseholdAsync_AdminInDifferentHousehold_ReturnsFalse()
    {
        var otherHouseholdId = Guid.NewGuid();
        await SeedTenantUserAsync(TenantRole.Member);
        await SeedHouseholdMemberAsync(Guid.NewGuid(), otherHouseholdId, HouseholdRole.Admin);

        var service = CreateService(_userId);
        Assert.False(await service.CanManageHouseholdAsync(_tenantId, _householdId, TestContext.Current.CancellationToken));
    }
}
