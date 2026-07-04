using System;

namespace Gatherstead.Data;

/// <summary>
/// Thrown by <see cref="Interceptors.AuditingSaveChangesInterceptor"/> when a save would write an entity
/// whose <c>TenantId</c> does not match the current tenant context — either a new row created under the
/// wrong tenant (<see cref="CrossTenantWriteReason.AddMismatch"/>) or an attempt to reassign an existing
/// row's tenant (<see cref="CrossTenantWriteReason.Reassignment"/>). Carries the offending entity/tenant
/// details so a catch site (e.g. the API's exception middleware) can record a security event.
/// </summary>
public class CrossTenantWriteBlockedException : InvalidOperationException
{
    public string EntityType { get; }
    public Guid EntityTenantId { get; }
    public Guid CurrentTenantId { get; }
    public CrossTenantWriteReason Reason { get; }

    public CrossTenantWriteBlockedException(
        string message,
        string entityType,
        Guid entityTenantId,
        Guid currentTenantId,
        CrossTenantWriteReason reason) : base(message)
    {
        EntityType = entityType;
        EntityTenantId = entityTenantId;
        CurrentTenantId = currentTenantId;
        Reason = reason;
    }
}

public enum CrossTenantWriteReason
{
    /// <summary>A new entity was created carrying a TenantId other than the current context's.</summary>
    AddMismatch,

    /// <summary>An existing entity's TenantId was changed away from its loaded value.</summary>
    Reassignment
}
