using Gatherstead.Db.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Gatherstead.Db.Interceptors;

public class AuditingSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserContext _currentUserContext;

    public AuditingSaveChangesInterceptor(ICurrentUserContext currentUserContext)
    {
        _currentUserContext = currentUserContext ?? throw new ArgumentNullException(nameof(currentUserContext));
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ApplyAuditing(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyAuditing(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplyAuditing(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var userId = _currentUserContext.UserId ?? throw new InvalidOperationException("Current user context did not provide a user identifier.");
        var utcNow = DateTimeOffset.UtcNow;

        foreach (var entry in context.ChangeTracker.Entries<IAuditableEntity>())
        {
            if (entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
            {
                continue;
            }

            switch (entry.State)
            {
                case EntityState.Added:
                    ApplyCreation(entry, userId, utcNow);
                    ApplyUpdate(entry, userId, utcNow);
                    break;
                case EntityState.Modified:
                    ApplyUpdate(entry, userId, utcNow);
                    ApplySoftDeleteIfNeeded(entry, userId, utcNow);
                    break;
                case EntityState.Deleted:
                    ConvertToSoftDelete(entry, userId, utcNow);
                    break;
            }
        }
    }

    private static void ApplyCreation(EntityEntry<IAuditableEntity> entry, Guid userId, DateTimeOffset timestamp)
    {
        entry.Entity.CreatedAt = timestamp;
        entry.Entity.CreatedByUserId = userId;
    }

    private static void ApplyUpdate(EntityEntry<IAuditableEntity> entry, Guid userId, DateTimeOffset timestamp)
    {
        entry.Entity.UpdatedAt = timestamp;
        entry.Entity.UpdatedByUserId = userId;
    }

    private static void ApplySoftDeleteIfNeeded(EntityEntry<IAuditableEntity> entry, Guid userId, DateTimeOffset timestamp)
    {
        var isDeletedProperty = entry.Property(e => e.IsDeleted);
        if (isDeletedProperty.IsModified && isDeletedProperty.CurrentValue)
        {
            entry.Entity.DeletedAt ??= timestamp;
            entry.Entity.DeletedByUserId ??= userId;
        }
    }

    private static void ConvertToSoftDelete(EntityEntry<IAuditableEntity> entry, Guid userId, DateTimeOffset timestamp)
    {
        entry.State = EntityState.Modified;
        entry.Entity.IsDeleted = true;
        entry.Entity.DeletedAt = timestamp;
        entry.Entity.DeletedByUserId = userId;
        entry.Entity.UpdatedAt = timestamp;
        entry.Entity.UpdatedByUserId = userId;
    }
}
