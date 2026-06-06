using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Contracts.Responses;

/// <summary>
/// Audit / lifecycle metadata for any entity deriving from <see cref="AuditableEntity"/>.
/// Returned only to authorized callers (Manager+) that explicitly request it via
/// <c>?includeAudit=true</c>; otherwise the owning DTO's <c>Audit</c> property is <c>null</c>.
/// </summary>
public record AuditInfo(
    DateTimeOffset CreatedAt,
    Guid CreatedByUserId,
    DateTimeOffset UpdatedAt,
    Guid? UpdatedByUserId,
    bool IsDeleted,
    DateTimeOffset? DeletedAt,
    Guid? DeletedByUserId);

public static class AuditInfoExtensions
{
    /// <summary>
    /// Projects an auditable entity's lifecycle metadata into an <see cref="AuditInfo"/>,
    /// or returns <c>null</c> when audit visibility has not been authorized for the request.
    /// Centralizes the strip-by-default rule so every service maps audit identically.
    /// </summary>
    public static AuditInfo? ToAuditInfo(this IAuditableEntity entity, bool includeAudit)
        => includeAudit
            ? new AuditInfo(
                entity.CreatedAt,
                entity.CreatedByUserId,
                entity.UpdatedAt,
                entity.UpdatedByUserId,
                entity.IsDeleted,
                entity.DeletedAt,
                entity.DeletedByUserId)
            : null;
}
