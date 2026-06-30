using Gatherstead.Api.Contracts.HouseholdUsers;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.HouseholdUsers;
using Gatherstead.Api.Tests.Fixtures;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Gatherstead.Api.Tests.Services;

public class HouseholdUserServiceTests : IAsyncLifetime
{
    private GathersteadDbContext _dbContext = null!;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _actorUserId = Guid.NewGuid();
    private readonly Guid _targetUserId = Guid.NewGuid();
    private readonly Guid _householdId = Guid.NewGuid();

    public async ValueTask InitializeAsync()
    {
        _dbContext = TestDbContextFactory.Create(tenantId: _tenantId, currentUserId: _actorUserId);
        _dbContext.Tenants.Add(new Tenant { Id = _tenantId, Name = "Test Tenant" });
        _dbContext.Users.Add(new User { Id = _actorUserId, ExternalId = "actor@test" });
        _dbContext.Users.Add(new User { Id = _targetUserId, ExternalId = "target@test" });
        _dbContext.Households.Add(new Household { Id = _householdId, TenantId = _tenantId, Name = "Test Household" });
        await _dbContext.SaveChangesAsync();
    }

    public ValueTask DisposeAsync()
    {
        _dbContext.Dispose();
        return ValueTask.CompletedTask;
    }

    private HouseholdUserService CreateService(bool canManageHousehold)
    {
        var tenantContext = Mock.Of<ICurrentTenantContext>(c => c.TenantId == _tenantId);
        var authService = Mock.Of<IMemberAuthorizationService>(s =>
            s.CanManageHouseholdAsync(_tenantId, _householdId, It.IsAny<CancellationToken>())
                == Task.FromResult(canManageHousehold));

        return new HouseholdUserService(_dbContext, tenantContext, authService, new FakeAuthCache());
    }

    private async Task SeedTenantUserAsync(Guid userId, CancellationToken ct = default, TenantRole role = TenantRole.Member)
    {
        _dbContext.TenantUsers.Add(new TenantUser { TenantId = _tenantId, UserId = userId, Role = role });
        await _dbContext.SaveChangesAsync(ct);
    }

    private async Task SeedHouseholdUserAsync(HouseholdRole role, bool deleted, CancellationToken ct = default)
    {
        _dbContext.HouseholdUsers.Add(new HouseholdUser
        {
            TenantId = _tenantId,
            HouseholdId = _householdId,
            UserId = _targetUserId,
            Role = role,
            IsDeleted = deleted,
            DeletedAt = deleted ? DateTimeOffset.UtcNow : null,
        });
        await _dbContext.SaveChangesAsync(ct);
    }

    // ── ListAsync ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task ListAsync_WithAccess_ReturnsHouseholdUsers()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedTenantUserAsync(_targetUserId, ct);
        await SeedHouseholdUserAsync(HouseholdRole.Member, deleted: false, ct);
        var service = CreateService(canManageHousehold: true);

        var result = await service.ListAsync(_tenantId, _householdId, ct);

        Assert.True(result.Successful);
        Assert.Single(result.Entity!);
    }

    [Fact]
    public async Task ListAsync_NoAccess_ReturnsError()
    {
        var service = CreateService(canManageHousehold: false);

        var result = await service.ListAsync(_tenantId, _householdId, TestContext.Current.CancellationToken);

        Assert.False(result.Successful);
    }

    // ── UpsertAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpsertAsync_NewUser_CreatesHouseholdUser()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedTenantUserAsync(_targetUserId, ct);
        var service = CreateService(canManageHousehold: true);

        var result = await service.UpsertAsync(
            _tenantId, _householdId, _targetUserId,
            new UpsertHouseholdUserRequest(HouseholdRole.Member), ct);

        Assert.True(result.Successful);
        Assert.Equal(HouseholdRole.Member, result.Entity!.Role);
        Assert.Equal(_targetUserId, result.Entity.UserId);
    }

    [Fact]
    public async Task UpsertAsync_UpdatesRole_WhenAlreadyExists()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedTenantUserAsync(_targetUserId, ct);
        await SeedHouseholdUserAsync(HouseholdRole.Member, deleted: false, ct);
        var service = CreateService(canManageHousehold: true);

        var result = await service.UpsertAsync(
            _tenantId, _householdId, _targetUserId,
            new UpsertHouseholdUserRequest(HouseholdRole.Manager), ct);

        Assert.True(result.Successful);
        Assert.Equal(HouseholdRole.Manager, result.Entity!.Role);
    }

    [Fact]
    public async Task UpsertAsync_RestoresSoftDeleted()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedTenantUserAsync(_targetUserId, ct);
        await SeedHouseholdUserAsync(HouseholdRole.Member, deleted: true, ct);
        var service = CreateService(canManageHousehold: true);

        var result = await service.UpsertAsync(
            _tenantId, _householdId, _targetUserId,
            new UpsertHouseholdUserRequest(HouseholdRole.Manager), ct);

        Assert.True(result.Successful);
        Assert.Equal(HouseholdRole.Manager, result.Entity!.Role);

        var dbRow = await _dbContext.HouseholdUsers
            .IgnoreQueryFilters()
            .Where(hu => hu.TenantId == _tenantId && hu.HouseholdId == _householdId && hu.UserId == _targetUserId)
            .SingleAsync(ct);
        Assert.False(dbRow.IsDeleted);
        Assert.Null(dbRow.DeletedAt);
    }

    [Fact]
    public async Task UpsertAsync_NonTenantMember_ReturnsError()
    {
        var service = CreateService(canManageHousehold: true);

        var result = await service.UpsertAsync(
            _tenantId, _householdId, _targetUserId,
            new UpsertHouseholdUserRequest(HouseholdRole.Member),
            TestContext.Current.CancellationToken);

        Assert.False(result.Successful);
        Assert.Contains(result.Messages, m => m.Type == MessageType.ERROR && m.Message.Contains("member of this tenant"));
    }

    [Fact]
    public async Task UpsertAsync_NoAccess_ReturnsError()
    {
        var service = CreateService(canManageHousehold: false);

        var result = await service.UpsertAsync(
            _tenantId, _householdId, _targetUserId,
            new UpsertHouseholdUserRequest(HouseholdRole.Member),
            TestContext.Current.CancellationToken);

        Assert.False(result.Successful);
    }

    // ── DeleteAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_SoftDeletes()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedTenantUserAsync(_targetUserId, ct);
        await SeedHouseholdUserAsync(HouseholdRole.Member, deleted: false, ct);
        var service = CreateService(canManageHousehold: true);

        var result = await service.DeleteAsync(_tenantId, _householdId, _targetUserId, ct);

        Assert.True(result.Successful);

        var dbRow = await _dbContext.HouseholdUsers
            .IgnoreQueryFilters()
            .Where(hu => hu.TenantId == _tenantId && hu.HouseholdId == _householdId && hu.UserId == _targetUserId)
            .SingleAsync(ct);
        Assert.True(dbRow.IsDeleted);
    }

    [Fact]
    public async Task DeleteAsync_AlreadyDeleted_ReturnsWarning()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedTenantUserAsync(_targetUserId, ct);
        await SeedHouseholdUserAsync(HouseholdRole.Member, deleted: true, ct);
        var service = CreateService(canManageHousehold: true);

        var result = await service.DeleteAsync(_tenantId, _householdId, _targetUserId, ct);

        Assert.False(result.Successful);
        Assert.Contains(result.Messages, m => m.Type == MessageType.WARNING);
    }

    [Fact]
    public async Task DeleteAsync_NotFound_ReturnsError()
    {
        var service = CreateService(canManageHousehold: true);

        var result = await service.DeleteAsync(_tenantId, _householdId, _targetUserId, TestContext.Current.CancellationToken);

        Assert.False(result.Successful);
        Assert.Contains(result.Messages, m => m.Type == MessageType.ERROR);
    }

    [Fact]
    public async Task DeleteAsync_NoAccess_ReturnsError()
    {
        var service = CreateService(canManageHousehold: false);

        var result = await service.DeleteAsync(_tenantId, _householdId, _targetUserId, TestContext.Current.CancellationToken);

        Assert.False(result.Successful);
    }
}
