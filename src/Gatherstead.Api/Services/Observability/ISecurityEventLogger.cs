using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Services.Observability;

public interface ISecurityEventLogger
{
    /// <summary>
    /// Emits a security event to both the OTel Activity (immediate) and the SecurityEvent DB table
    /// (durable). DB failures degrade gracefully — they are logged but never propagate to the caller.
    /// </summary>
    Task LogAsync(
        SecurityEventType eventType,
        SecurityEventSeverity severity,
        string resource = "",
        string? detail = null,
        Guid? tenantId = null,
        Guid? userId = null,
        CancellationToken cancellationToken = default);
}
