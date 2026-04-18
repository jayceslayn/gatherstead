namespace Gatherstead.Data.Entities;

/// <summary>
/// Immutable audit record for security-relevant events. Persisted for long-term retention
/// beyond the 90-day App Insights window. Never soft-deleted; rows are append-only.
/// </summary>
public sealed class SecurityEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public SecurityEventType EventType { get; init; }
    public SecurityEventSeverity Severity { get; init; }

    /// <summary>Null for pre-auth events (e.g. AuthFailure before tenant is known).</summary>
    public Guid? TenantId { get; init; }

    /// <summary>Null for pre-auth events.</summary>
    public Guid? UserId { get; init; }

    /// <summary>W3C TraceId string from Activity.Current — links to the App Insights trace.</summary>
    public string CorrelationId { get; init; } = "";

    /// <summary>SHA-256(IP + daily salt). Rotates daily; never stores raw IP.</summary>
    public byte[]? IpAddressHash { get; init; }

    /// <summary>SHA-256(UserAgent + daily salt).</summary>
    public byte[]? UserAgentHash { get; init; }

    /// <summary>Short descriptor of the affected resource, e.g. "HouseholdMember:{id}".</summary>
    public string Resource { get; init; } = "";

    /// <summary>JSON object with additional IDs / enum values. No PII.</summary>
    public string? Detail { get; init; }
}
