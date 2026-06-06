namespace Gatherstead.Api.Contracts.Responses;

/// <summary>
/// Indicates whether the current request has been authorized to receive entity audit
/// metadata. Set by <c>RequireTenantAccessAttribute</c> when a Manager+ (or App Admin)
/// caller requests <c>?includeAudit=true</c>. Services use this to decide whether to populate
/// the <c>Audit</c> block on response DTOs. Co-located with <see cref="AuditInfo"/> because
/// both concern response shaping; the HTTP-backed implementation lives in the Security layer.
/// </summary>
public interface IAuditVisibilityContext
{
    bool IncludeAudit { get; }
}
