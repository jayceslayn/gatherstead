using Gatherstead.Data.Entities;
using Gatherstead.Data.Interceptors;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Linq.Expressions;

namespace Gatherstead.Data;

public class GathersteadDbContext : DbContext
{
    private readonly AuditingSaveChangesInterceptor _auditingSaveChangesInterceptor;
    private readonly Guid? _tenantId;
    private readonly IIncludeDeletedContext? _includeDeletedContext;

    private bool IncludeDeleted => _includeDeletedContext?.IncludeDeleted ?? false;

    public GathersteadDbContext(
        DbContextOptions<GathersteadDbContext> options,
        AuditingSaveChangesInterceptor auditingSaveChangesInterceptor,
        ICurrentTenantContext currentTenantContext,
        IIncludeDeletedContext? includeDeletedContext = null) : base(options)
    {
        _auditingSaveChangesInterceptor = auditingSaveChangesInterceptor
            ?? throw new ArgumentNullException(nameof(auditingSaveChangesInterceptor));
        _tenantId = currentTenantContext?.TenantId;
        _includeDeletedContext = includeDeletedContext;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<TenantUser> TenantUsers => Set<TenantUser>();
    public DbSet<Household> Households => Set<Household>();
    public DbSet<HouseholdMember> HouseholdMembers => Set<HouseholdMember>();
    public DbSet<Property> Properties => Set<Property>();
    public DbSet<Accommodation> Accommodations => Set<Accommodation>();
    public DbSet<AccommodationIntent> AccommodationIntents => Set<AccommodationIntent>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<MealTemplate> MealTemplates => Set<MealTemplate>();
    public DbSet<MealPlan> MealPlans => Set<MealPlan>();
    public DbSet<MealIntent> MealIntents => Set<MealIntent>();
    public DbSet<MealAttendance> MealAttendances => Set<MealAttendance>();
    public DbSet<ChoreTemplate> ChoreTemplates => Set<ChoreTemplate>();
    public DbSet<ChorePlan> ChorePlans => Set<ChorePlan>();
    public DbSet<ChoreIntent> ChoreIntents => Set<ChoreIntent>();
    public DbSet<EventAttendance> EventAttendances => Set<EventAttendance>();
    public DbSet<RevokedToken> RevokedTokens => Set<RevokedToken>();
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<ContactMethod> ContactMethods => Set<ContactMethod>();
    public DbSet<MemberAttribute> MemberAttributes => Set<MemberAttribute>();
    public DbSet<MemberRelationship> MemberRelationships => Set<MemberRelationship>();
    public DbSet<DietaryProfile> DietaryProfiles => Set<DietaryProfile>();
    public DbSet<SecurityEvent> SecurityEvents => Set<SecurityEvent>();
    public DbSet<MemberPreferenceSettings> MemberPreferenceSettings => Set<MemberPreferenceSettings>();
    public DbSet<TenantNotificationPolicy> TenantNotificationPolicies => Set<TenantNotificationPolicy>();
    public DbSet<MemberNotificationPreference> MemberNotificationPreferences => Set<MemberNotificationPreference>();
    public DbSet<UserNotificationPreference> UserNotificationPreferences => Set<UserNotificationPreference>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

        optionsBuilder.AddInterceptors(_auditingSaveChangesInterceptor);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        if (Database.IsSqlServer())
        {
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                // SecurityEvent is append-only; temporal history adds overhead without benefit.
                if (entityType.ClrType == typeof(SecurityEvent))
                    continue;
                modelBuilder.Entity(entityType.ClrType).ToTable(tb => tb.IsTemporal());
            }
        }

        modelBuilder.Entity<SecurityEvent>(b =>
        {
            b.ToTable("SecurityEvents", "security");
            b.HasIndex(e => e.OccurredAt).HasDatabaseName("IX_SecurityEvent_OccurredAt");
            b.HasIndex(e => new { e.TenantId, e.OccurredAt }).HasDatabaseName("IX_SecurityEvent_TenantId_OccurredAt");
            b.HasIndex(e => new { e.EventType, e.OccurredAt }).HasDatabaseName("IX_SecurityEvent_EventType_OccurredAt");
            b.HasIndex(e => e.UserId).HasDatabaseName("IX_SecurityEvent_UserId");
            b.HasIndex(e => e.CorrelationId).HasDatabaseName("IX_SecurityEvent_CorrelationId");
        });

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

        modelBuilder.Entity<RevokedToken>(b =>
        {
            b.ToTable("RevokedTokens", "security");
            b.HasIndex(p => p.Jti)
                .HasDatabaseName("IX_RevokedToken_Jti");

            // Index on ExpiresAt for efficient cleanup queries
            b.HasIndex(p => p.ExpiresAt)
                .HasDatabaseName("IX_RevokedToken_ExpiresAt");

            // Composite index for tenant-specific queries
            b.HasIndex(p => new { p.TenantId, p.UserId })
                .HasDatabaseName("IX_RevokedToken_TenantUser");
        });

        modelBuilder.Entity<HouseholdMember>(b =>
        {
            b.HasIndex(hm => new { hm.TenantId, hm.UserId })
                .HasDatabaseName("IX_HouseholdMember_TenantUser");
        });

        modelBuilder.Entity<MemberPreferenceSettings>(b =>
        {
            b.HasIndex(mps => new { mps.TenantId, mps.HouseholdMemberId })
                .IsUnique()
                .HasDatabaseName("IX_MemberPreferenceSettings_TenantMember");
        });

        modelBuilder.Entity<TenantNotificationPolicy>(b =>
        {
            b.HasIndex(tp => new { tp.TenantId, tp.Channel, tp.Category })
                .IsUnique()
                .HasDatabaseName("IX_TenantNotificationPolicy_TenantChannelCategory");
        });

        modelBuilder.Entity<MemberNotificationPreference>(b =>
        {
            b.HasIndex(mp => new { mp.TenantId, mp.HouseholdMemberId, mp.Channel, mp.Category })
                .IsUnique()
                .HasDatabaseName("IX_MemberNotificationPreference_UniquePreference");
        });

        modelBuilder.Entity<UserNotificationPreference>(b =>
        {
            b.HasIndex(up => new { up.UserId, up.Channel, up.Category })
                .IsUnique()
                .HasDatabaseName("IX_UserNotificationPreference_UserChannelCategory");
        });


        // Configure MemberRelationship to HouseholdMember relationship
        modelBuilder.Entity<MemberRelationship>(b =>
        {
            b.HasOne(mr => mr.HouseholdMember)
                .WithMany(hm => hm.Relationships)
                .HasForeignKey(mr => mr.HouseholdMemberId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(mr => mr.RelatedMember)
                .WithMany()
                .HasForeignKey(mr => mr.RelatedMemberId)
                .OnDelete(DeleteBehavior.Restrict);
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

        // Soft-delete filter: IncludeDeleted || !entity.IsDeleted
        // EF Core re-evaluates the property reference per query, so the filter is composable.
        // IncludeDeleted delegates to IIncludeDeletedContext, which reads from HttpContext.Items
        // set by RequireTenantAccessAttribute after authorization completes.
        var includeDeletedField = Expression.Property(Expression.Constant(this), nameof(IncludeDeleted));
        var isDeletedProperty = Expression.Property(parameter, nameof(IAuditableEntity.IsDeleted));
        Expression filterBody = Expression.OrElse(includeDeletedField, Expression.Not(isDeletedProperty));

        var tenantIdProperty = typeof(TEntity).GetProperty("TenantId");
        if (tenantIdProperty?.PropertyType == typeof(Guid))
        {
            var tenantIdAccess = Expression.Property(parameter, tenantIdProperty);
            var tenantIdAsNullable = Expression.Convert(tenantIdAccess, typeof(Guid?));
            var tenantIdField = Expression.Field(Expression.Constant(this), nameof(_tenantId));
            var tenantIdComparison = Expression.Equal(tenantIdAsNullable, tenantIdField);
            filterBody = Expression.AndAlso(filterBody, tenantIdComparison);
        }

        var lambda = Expression.Lambda<Func<TEntity, bool>>(filterBody, parameter);
        entityBuilder.HasQueryFilter(lambda);
    }
}
