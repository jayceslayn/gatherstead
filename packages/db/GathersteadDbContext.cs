using Microsoft.EntityFrameworkCore;
using Gatherstead.Db.Encryption;
using Gatherstead.Db.Entities;
using Gatherstead.Db.Interceptors;

namespace Gatherstead.Db;

public class GathersteadDbContext : DbContext
{
    private readonly AuditingSaveChangesInterceptor _auditingSaveChangesInterceptor;

    public GathersteadDbContext(
        DbContextOptions<GathersteadDbContext> options,
        AuditingSaveChangesInterceptor auditingSaveChangesInterceptor) : base(options)
    {
        _auditingSaveChangesInterceptor = auditingSaveChangesInterceptor
            ?? throw new ArgumentNullException(nameof(auditingSaveChangesInterceptor));
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

        // Composite keys
        modelBuilder.Entity<TenantUser>().HasKey(tu => new { tu.TenantId, tu.UserId });

        // Encryption conversions
        modelBuilder.Entity<HouseholdMember>(b =>
        {
            b.Property(p => p.Name)
                .HasConversion<EncryptedStringConverter>()
                .HasColumnType("bytea");
            b.Property(p => p.BirthDate)
                .HasConversion<EncryptedDateOnlyConverter>()
                .HasColumnType("bytea");
            b.Property(p => p.DietaryNotes)
                .HasConversion<EncryptedStringConverter>()
                .HasColumnType("bytea");
        });

        modelBuilder.Entity<Resource>(b =>
        {
            b.Property(p => p.Notes)
                .HasConversion<EncryptedStringConverter>()
                .HasColumnType("bytea");
        });

        modelBuilder.Entity<MealPlan>(b =>
        {
            b.Property(p => p.Notes)
                .HasConversion<EncryptedStringConverter>()
                .HasColumnType("bytea");
        });

        modelBuilder.Entity<MealIntent>(b =>
        {
            b.Property(p => p.Notes)
                .HasConversion<EncryptedStringConverter>()
                .HasColumnType("bytea");
        });

        modelBuilder.Entity<StayIntent>(b =>
        {
            b.Property(p => p.Notes)
                .HasConversion<EncryptedStringConverter>()
                .HasColumnType("bytea");
        });

        modelBuilder.Entity<ChoreTemplate>(b =>
        {
            b.Property(p => p.Notes)
                .HasConversion<EncryptedStringConverter>()
                .HasColumnType("bytea");
        });

        modelBuilder.Entity<ChoreTask>(b =>
        {
            b.Property(p => p.Notes)
                .HasConversion<EncryptedStringConverter>()
                .HasColumnType("bytea");
        });

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            modelBuilder.Entity(entityType.ClrType).ToTable(tb => tb.IsTemporal());
        }
    }
}
