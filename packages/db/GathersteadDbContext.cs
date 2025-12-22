using Gatherstead.Db.Entities;
using Gatherstead.Db.Interceptors;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Linq.Expressions;

namespace Gatherstead.Db;

public class GathersteadDbContext : DbContext
{
    private readonly AuditingSaveChangesInterceptor _auditingSaveChangesInterceptor;
    private readonly Guid? _tenantId;

    public GathersteadDbContext(
        DbContextOptions<GathersteadDbContext> options,
        AuditingSaveChangesInterceptor auditingSaveChangesInterceptor,
        ICurrentTenantContext currentTenantContext) : base(options)
    {
        _auditingSaveChangesInterceptor = auditingSaveChangesInterceptor
            ?? throw new ArgumentNullException(nameof(auditingSaveChangesInterceptor));
        _tenantId = currentTenantContext?.TenantId;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<TenantUser> TenantUsers => Set<TenantUser>();
    public DbSet<Household> Households => Set<Household>();
    public DbSet<HouseholdMember> HouseholdMembers => Set<HouseholdMember>();
    public DbSet<Property> Properties => Set<Property>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Resource> Resources => Set<Resource>();
    public DbSet<MealPlan> MealPlans => Set<MealPlan>();
    public DbSet<MealIntent> MealIntents => Set<MealIntent>();
    public DbSet<StayIntent> StayIntents => Set<StayIntent>();
    public DbSet<ChoreTemplate> ChoreTemplates => Set<ChoreTemplate>();
    public DbSet<ChoreTask> ChoreTasks => Set<ChoreTask>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

        optionsBuilder.AddInterceptors(_auditingSaveChangesInterceptor);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            modelBuilder.Entity(entityType.ClrType).ToTable(tb => tb.IsTemporal());
        }

        modelBuilder.Entity<ContactMethod>(b =>
        {
            b.HasIndex(p => new { p.TenantId, p.HouseholdMemberId })
                .HasDatabaseName("IX_Contact_PrimaryPerMember")
                .HasFilter("[IsPrimary] = 1");
        });

        modelBuilder.Entity<Address>(b =>
        {
            b.HasIndex(p => new { p.TenantId, p.HouseholdMemberId })
                .HasDatabaseName("IX_Address_PrimaryPerMember")
                .HasFilter("[IsPrimary] = 1");
        });

        foreach (var foreignKey in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
        {
            foreignKey.DeleteBehavior = DeleteBehavior.Restrict;
        }

        ApplyGlobalFilters(modelBuilder);
    }

    private void ApplyGlobalFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(IAuditableEntity).IsAssignableFrom(entityType.ClrType))
            {
                var applyFilterMethod = typeof(GathersteadDbContext)
                    .GetMethod(nameof(ApplyAuditableFilters), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
                var genericMethod = applyFilterMethod.MakeGenericMethod(entityType.ClrType);
                genericMethod.Invoke(this, new object[] { modelBuilder });
            }
        }
    }

    private void ApplyAuditableFilters<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, IAuditableEntity
    {
        var entityBuilder = modelBuilder.Entity<TEntity>();

        var parameter = Expression.Parameter(typeof(TEntity), "entity");
        Expression filterBody = Expression.Not(Expression.Property(parameter, nameof(IAuditableEntity.IsDeleted)));

        var tenantIdProperty = typeof(TEntity).GetProperty("TenantId");
        if (tenantIdProperty?.PropertyType == typeof(Guid))
        {
            var tenantIdAccess = Expression.Property(parameter, tenantIdProperty);
            var tenantIdAsNullable = Expression.Convert(tenantIdAccess, typeof(Guid?));
            var tenantIdComparison = Expression.Equal(tenantIdAsNullable, Expression.Constant(_tenantId, typeof(Guid?)));
            filterBody = Expression.AndAlso(filterBody, tenantIdComparison);
        }

        var lambda = Expression.Lambda<Func<TEntity, bool>>(filterBody, parameter);
        entityBuilder.HasQueryFilter(lambda);
    }
}
