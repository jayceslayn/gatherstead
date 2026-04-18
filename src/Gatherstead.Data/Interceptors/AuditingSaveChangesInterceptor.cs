using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Gatherstead.Data.Interceptors;

public class AuditingSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ICurrentTenantContext _currentTenantContext;
    private readonly ILogger<AuditingSaveChangesInterceptor> _logger;

    public AuditingSaveChangesInterceptor(
        ICurrentUserContext currentUserContext,
        ICurrentTenantContext currentTenantContext,
        ILogger<AuditingSaveChangesInterceptor> logger)
    {
        _currentUserContext = currentUserContext ?? throw new ArgumentNullException(nameof(currentUserContext));
        _currentTenantContext = currentTenantContext ?? throw new ArgumentNullException(nameof(currentTenantContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

        ValidateTenantId(context);
    }

    private void ValidateTenantId(DbContext context)
    {
        var currentTenantId = _currentTenantContext.TenantId;
        if (!currentTenantId.HasValue)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State != EntityState.Added)
            {
                continue;
            }

            var tenantIdProperty = entry.Properties
                .FirstOrDefault(p => p.Metadata.Name == "TenantId" && p.CurrentValue is Guid);

            if (tenantIdProperty is null)
            {
                continue;
            }

            var entityTenantId = (Guid)tenantIdProperty.CurrentValue!;
            if (entityTenantId != currentTenantId.Value)
            {
                _logger.LogCritical(
                    "Cross-tenant write blocked: entity {EntityType} has TenantId {EntityTenantId} " +
                    "but current tenant context is {CurrentTenantId}. UserId: {UserId}",
                    entry.Entity.GetType().Name,
                    entityTenantId,
                    currentTenantId.Value,
                    _currentUserContext.UserId);

                throw new InvalidOperationException(
                    $"Entity '{entry.Entity.GetType().Name}' has TenantId '{entityTenantId}' " +
                    $"but the current tenant context is '{currentTenantId.Value}'. " +
                    $"Cross-tenant writes are not permitted.");
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
