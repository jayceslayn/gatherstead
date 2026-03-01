using Gatherstead.Api.Tests.Fixtures;
using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Tests.Interceptors;

public class AuditingSaveChangesInterceptorTenantValidationTests : IDisposable
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private Gatherstead.Data.GathersteadDbContext? _dbContext;

    public void Dispose()
    {
        _dbContext?.Dispose();
    }

    private async Task SeedTenantAndUser(Gatherstead.Data.GathersteadDbContext dbContext)
    {
        var user = new User { Id = _userId, ExternalId = $"ext-{_userId}" };
        var tenant = new Tenant { Id = _tenantId, Name = "Test Tenant" };
        dbContext.Users.Add(user);
        dbContext.Tenants.Add(tenant);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var tenantUser = new TenantUser { TenantId = _tenantId, UserId = _userId, Role = TenantRole.Owner };
        dbContext.TenantUsers.Add(tenantUser);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SaveChanges_AddedEntityWithMatchingTenantId_Succeeds()
    {
        _dbContext = TestDbContextFactory.Create(tenantId: _tenantId, currentUserId: _userId);
        await SeedTenantAndUser(_dbContext);

        var household = new Household
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            Name = "Test Household"
        };

        _dbContext.Households.Add(household);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var saved = await _dbContext.Households.FindAsync([household.Id], TestContext.Current.CancellationToken);
        Assert.NotNull(saved);
        Assert.Equal(_tenantId, saved.TenantId);
    }

    [Fact]
    public async Task SaveChanges_AddedEntityWithMismatchedTenantId_ThrowsInvalidOperationException()
    {
        _dbContext = TestDbContextFactory.Create(tenantId: _tenantId, currentUserId: _userId);
        await SeedTenantAndUser(_dbContext);

        var wrongTenantId = Guid.NewGuid();
        var household = new Household
        {
            Id = Guid.NewGuid(),
            TenantId = wrongTenantId,
            Name = "Wrong Tenant Household"
        };

        _dbContext.Households.Add(household);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken));

        Assert.Contains("Cross-tenant writes are not permitted", exception.Message);
        Assert.Contains(wrongTenantId.ToString(), exception.Message);
        Assert.Contains(_tenantId.ToString(), exception.Message);
    }

    [Fact]
    public async Task SaveChanges_NullTenantContext_AllowsWriteWithoutValidation()
    {
        _dbContext = TestDbContextFactory.Create(tenantId: null, currentUserId: _userId);

        var user = new User { Id = _userId, ExternalId = $"ext-{_userId}" };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var newTenantId = Guid.NewGuid();
        var tenant = new Tenant { Id = newTenantId, Name = "New Tenant" };
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var tenantUser = new TenantUser { TenantId = newTenantId, UserId = _userId, Role = TenantRole.Owner };
        _dbContext.TenantUsers.Add(tenantUser);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var saved = await _dbContext.TenantUsers.FindAsync([newTenantId, _userId], TestContext.Current.CancellationToken);
        Assert.NotNull(saved);
    }
}
