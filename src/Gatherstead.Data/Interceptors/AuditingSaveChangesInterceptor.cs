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
            return;

        var utcNow = DateTimeOffset.UtcNow;
        // UserId is resolved lazily — only required when there are auditable entries to stamp.
        // This allows non-IAuditableEntity writes (e.g. SecurityEvent) to succeed without a user context.
        Guid? userId = null;

        foreach (var entry in context.ChangeTracker.Entries<IAuditableEntity>())
        {
            if (entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
                continue;

            userId ??= _currentUserContext.UserId
                ?? throw new InvalidOperationException("Current user context did not provide a user identifier.");

            switch (entry.State)
            {
                case EntityState.Added:
                    ApplyCreation(entry, userId.Value, utcNow);
                    ApplyUpdate(entry, userId.Value, utcNow);
                    break;
                case EntityState.Modified:
                    ApplyUpdate(entry, userId.Value, utcNow);
                    ApplySoftDeleteIfNeeded(entry, userId.Value, utcNow);
                    break;
                case EntityState.Deleted:
                    ConvertToSoftDelete(entry, userId.Value, utcNow);
                    // TODO: Cascade soft delete to FK-related entities that also implement IAuditableEntity?
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
            if (entry.State != EntityState.Added && entry.State != EntityState.Modified)
            {
                continue;
            }

            var tenantIdProperty = entry.Properties
                .FirstOrDefault(p => p.Metadata.Name == "TenantId" && p.CurrentValue is Guid);

            if (tenantIdProperty is null)
            {
                continue;
            }

            if (entry.State == EntityState.Modified)
            {
                // Block TenantId reassignment — the value must not change from what was loaded.
                if (tenantIdProperty.OriginalValue is Guid originalTenantId &&
                    tenantIdProperty.CurrentValue is Guid updatedTenantId &&
                    originalTenantId != updatedTenantId)
                {
                    _logger.LogCritical(
                        "Cross-tenant reassignment blocked: entity {EntityType} TenantId changed from {OriginalTenantId} " +
                        "to {NewTenantId}. UserId: {UserId}",
                        entry.Entity.GetType().Name,
                        originalTenantId,
                        updatedTenantId,
                        _currentUserContext.UserId);

                    throw new CrossTenantWriteBlockedException(
                        $"Entity '{entry.Entity.GetType().Name}' TenantId cannot be changed from '{originalTenantId}' " +
                        $"to '{updatedTenantId}'. Cross-tenant reassignment is not permitted.",
                        entry.Entity.GetType().Name,
                        updatedTenantId,
                        originalTenantId,
                        CrossTenantWriteReason.Reassignment);
                }
                continue;
            }

            // EntityState.Added: entity TenantId must match the current request's tenant context.
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

                throw new CrossTenantWriteBlockedException(
                    $"Entity '{entry.Entity.GetType().Name}' has TenantId '{entityTenantId}' " +
                    $"but the current tenant context is '{currentTenantId.Value}'. " +
                    $"Cross-tenant writes are not permitted.",
                    entry.Entity.GetType().Name,
                    entityTenantId,
                    currentTenantId.Value,
                    CrossTenantWriteReason.AddMismatch);
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
        if (!isDeletedProperty.IsModified)
            return;

        if (isDeletedProperty.CurrentValue)
        {
            entry.Entity.DeletedAt ??= timestamp;
            entry.Entity.DeletedByUserId ??= userId;
        }
        else
        {
            // Revive: clear the deletion stamp, otherwise a later soft delete would keep the stale
            // timestamp/user (the ??= above) and misattribute the new deletion.
            entry.Entity.DeletedAt = null;
            entry.Entity.DeletedByUserId = null;
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
