using System.Diagnostics.Metrics;

namespace Gatherstead.Api.Observability;

/// <summary>
/// Business-level metric counters exported via the shared Meter.
/// All methods are thread-safe; counters are safe to call from any context.
/// Tags follow OTel dotted-key conventions and are non-PII (IDs and enum names only).
/// </summary>
public static class GathersteadMetrics
{
    private static readonly Counter<long> TenantCreated =
        GathersteadTelemetry.Meter.CreateCounter<long>(
            "gatherstead.tenant.created",
            description: "Number of tenants created.");

    private static readonly Counter<long> HouseholdCreated =
        GathersteadTelemetry.Meter.CreateCounter<long>(
            "gatherstead.household.created",
            description: "Number of households created.");

    private static readonly Counter<long> MemberCreated =
        GathersteadTelemetry.Meter.CreateCounter<long>(
            "gatherstead.member.created",
            description: "Number of household members created.");

    private static readonly Counter<long> AuthzDenied =
        GathersteadTelemetry.Meter.CreateCounter<long>(
            "gatherstead.authz.denied",
            description: "Number of authorization denials.");

    private static readonly Counter<long> AuthnFailed =
        GathersteadTelemetry.Meter.CreateCounter<long>(
            "gatherstead.authn.failed",
            description: "Number of authentication failures.");

    private static readonly Counter<long> SoftDeleted =
        GathersteadTelemetry.Meter.CreateCounter<long>(
            "gatherstead.soft_delete",
            description: "Number of soft-deleted entities.");

    private static readonly Counter<long> SecurityEventRecorded =
        GathersteadTelemetry.Meter.CreateCounter<long>(
            "gatherstead.security_event",
            description: "Number of security events recorded.");

    public static void RecordTenantCreated() =>
        TenantCreated.Add(1);

    public static void RecordHouseholdCreated(Guid tenantId) =>
        HouseholdCreated.Add(1, new KeyValuePair<string, object?>("tenant.id", tenantId.ToString()));

    public static void RecordMemberCreated(Guid tenantId, Guid householdId) =>
        MemberCreated.Add(1,
            new KeyValuePair<string, object?>("tenant.id", tenantId.ToString()),
            new KeyValuePair<string, object?>("household.id", householdId.ToString()));

    public static void RecordAuthzDenied(string reason, Guid? tenantId = null) =>
        AuthzDenied.Add(1,
            new KeyValuePair<string, object?>("reason", reason),
            new KeyValuePair<string, object?>("tenant.id", tenantId?.ToString()));

    public static void RecordAuthnFailed(string reason) =>
        AuthnFailed.Add(1, new KeyValuePair<string, object?>("reason", reason));

    public static void RecordSoftDelete(string entityType, Guid? tenantId = null) =>
        SoftDeleted.Add(1,
            new KeyValuePair<string, object?>("entity_type", entityType),
            new KeyValuePair<string, object?>("tenant.id", tenantId?.ToString()));

    public static void RecordSecurityEvent(string eventType, string severity) =>
        SecurityEventRecorded.Add(1,
            new KeyValuePair<string, object?>("event_type", eventType),
            new KeyValuePair<string, object?>("severity", severity));
}
