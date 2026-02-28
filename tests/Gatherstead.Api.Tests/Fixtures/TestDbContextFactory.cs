using Gatherstead.Data;
using Gatherstead.Data.Interceptors;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Gatherstead.Api.Tests.Fixtures;

public static class TestDbContextFactory
{
    public static GathersteadDbContext Create(
        Guid? tenantId = null,
        bool includeDeleted = false,
        Guid? currentUserId = null)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<GathersteadDbContext>()
            .UseSqlite(connection)
            .Options;

        var userId = currentUserId ?? Guid.NewGuid();
        var userContext = Mock.Of<ICurrentUserContext>(c => c.UserId == userId);
        var interceptor = new AuditingSaveChangesInterceptor(userContext);
        var tenantContext = Mock.Of<ICurrentTenantContext>(c => c.TenantId == tenantId);
        var deleteContext = Mock.Of<IIncludeDeletedContext>(c => c.IncludeDeleted == includeDeleted);

        var context = new GathersteadDbContext(options, interceptor, tenantContext, deleteContext);
        context.Database.EnsureCreated();

        return context;
    }
}
