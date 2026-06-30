using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Gatherstead.Data.Interceptors;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Gatherstead.Api.Tests.Data;

/// <summary>
/// Pins the composition of the two named global query filters (<see cref="GathersteadDbContext.SoftDeleteFilter"/>
/// and <see cref="GathersteadDbContext.TenantFilter"/>) directly, independent of any service. This is the
/// security-critical contract: disabling one dimension must leave the other enforced.
/// </summary>
public class QueryFilterCompositionTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();
    private readonly Guid _activeInA = Guid.NewGuid();
    private readonly Guid _deletedInA = Guid.NewGuid();
    private readonly Guid _activeInB = Guid.NewGuid();

    public QueryFilterCompositionTests()
    {
        // One shared in-memory database so a null-context seeder and a tenant-scoped reader see the
        // same rows. Seeding uses a null tenant context (the auditing interceptor only enforces its
        // cross-tenant write guard when a tenant context is present).
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var seed = CreateContext(tenantId: null);
        seed.Database.EnsureCreated();
        seed.Tenants.Add(new Tenant { Id = _tenantA, Name = "Tenant A" });
        seed.Tenants.Add(new Tenant { Id = _tenantB, Name = "Tenant B" });
        seed.Users.Add(new User { Id = _activeInA, ExternalId = "active-a@test" });
        seed.Users.Add(new User { Id = _deletedInA, ExternalId = "deleted-a@test" });
        seed.Users.Add(new User { Id = _activeInB, ExternalId = "active-b@test" });
        seed.TenantUsers.Add(new TenantUser { TenantId = _tenantA, UserId = _activeInA, Role = TenantRole.Owner });
        seed.TenantUsers.Add(new TenantUser { TenantId = _tenantB, UserId = _activeInB, Role = TenantRole.Owner });
        var deleted = new TenantUser { TenantId = _tenantA, UserId = _deletedInA, Role = TenantRole.Member };
        seed.TenantUsers.Add(deleted);
        seed.SaveChanges();
        deleted.IsDeleted = true;
        seed.SaveChanges();
    }

    private GathersteadDbContext CreateContext(Guid? tenantId, bool includeDeleted = false)
    {
        var options = new DbContextOptionsBuilder<GathersteadDbContext>()
            .UseSqlite(_connection)
            .Options;
        var userContext = Mock.Of<ICurrentUserContext>(c => c.UserId == Guid.NewGuid());
        var tenantContext = Mock.Of<ICurrentTenantContext>(c => c.TenantId == tenantId);
        var interceptor = new AuditingSaveChangesInterceptor(
            userContext, tenantContext, NullLogger<AuditingSaveChangesInterceptor>.Instance);
        var deleteContext = Mock.Of<IIncludeDeletedContext>(c => c.IncludeDeleted == includeDeleted);
        return new GathersteadDbContext(options, interceptor, tenantContext, deleteContext);
    }

    public void Dispose()
    {
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task BothFilters_OnlyActiveCurrentTenantRows()
    {
        using var ctx = CreateContext(_tenantA);

        var rows = await ctx.TenantUsers.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken);

        var tu = Assert.Single(rows);
        Assert.Equal(_activeInA, tu.UserId);
    }

    [Fact]
    public async Task IgnoreSoftDeleteFilter_KeepsTenantIsolation()
    {
        using var ctx = CreateContext(_tenantA);

        var rows = await ctx.TenantUsers
            .IgnoreQueryFilters([GathersteadDbContext.SoftDeleteFilter])
            .AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken);

        // Soft-deleted row is now visible, but the other tenant's row stays hidden.
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.Equal(_tenantA, r.TenantId));
        Assert.Contains(rows, r => r.UserId == _deletedInA);
    }

    [Fact]
    public async Task IgnoreTenantFilter_KeepsSoftDelete()
    {
        using var ctx = CreateContext(_tenantA);

        var rows = await ctx.TenantUsers
            .IgnoreQueryFilters([GathersteadDbContext.TenantFilter])
            .AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken);

        // The other tenant's active row is now visible, but the soft-deleted row stays hidden.
        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.TenantId == _tenantB);
        Assert.DoesNotContain(rows, r => r.UserId == _deletedInA);
    }

    [Fact]
    public async Task IgnoreAllFilters_ReturnsEverything()
    {
        using var ctx = CreateContext(_tenantA);

        var rows = await ctx.TenantUsers
            .IgnoreQueryFilters()
            .AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(3, rows.Count);
    }
}
