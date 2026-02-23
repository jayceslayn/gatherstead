using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Gatherstead.Data.Interceptors;

namespace Gatherstead.Data;

/// <summary>
/// Design-time factory for creating GathersteadDbContext instances during migrations
/// </summary>
public class GathersteadDbContextFactory : IDesignTimeDbContextFactory<GathersteadDbContext>
{
    public GathersteadDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<GathersteadDbContext>();

        // Use a dummy connection string for design-time operations
        optionsBuilder.UseSqlServer("Server=localhost;Database=GathersteadDesignTime;Integrated Security=true;TrustServerCertificate=True;");

        // Create dummy services for the required dependencies
        var currentUserContext = new DummyCurrentUserContext();
        var currentTenantContext = new DummyCurrentTenantContext();
        var auditingInterceptor = new AuditingSaveChangesInterceptor(currentUserContext);

        return new GathersteadDbContext(optionsBuilder.Options, auditingInterceptor, currentTenantContext);
    }

    private class DummyCurrentUserContext : ICurrentUserContext
    {
        public Guid? UserId => null;
    }

    private class DummyCurrentTenantContext : ICurrentTenantContext
    {
        public Guid? TenantId => null;
    }
}
