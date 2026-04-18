using OpenTelemetry;
using OpenTelemetry.Logs;

namespace Gatherstead.Api.Observability;

/// <summary>
/// OTel log processor that redacts any structured log attribute not in the explicit
/// allowlist before export to Azure Monitor. Acts as a belt-and-suspenders guard
/// so that a developer accidentally logging an entity field does not leak PII.
///
/// When any attribute is redacted the FormattedMessage is also cleared — the
/// message template (stored in {OriginalFormat}) is kept for shape-of-log debugging
/// without exposing the substituted values.
///
/// See docs/OBSERVABILITY.md for the full logging contract and allowlist rationale.
/// </summary>
public sealed class PiiRedactionLogProcessor : BaseProcessor<LogRecord>
{
    public const string RedactedValue = "[redacted]";

    // Explicit allowlist. Key comparison is case-insensitive.
    // Keys not on this list are redacted to [redacted] before export.
    private static readonly HashSet<string> AllowedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        // ── Application identity dimensions (GUIDs / internal IDs only) ─────────
        "TenantId",       "tenant_id",       "tenant.id",
        "UserId",         "user_id",         "user.id",
        "MemberId",       "member_id",       "member.id",
        "HouseholdId",    "household_id",    "household.id",
        "EventId",        "event_id",        "event.id",
        "ResourceId",     "resource_id",     "resource.id",
        "CorrelationId",  "correlation_id",  "correlation.id",
        "EntityTenantId", "CurrentTenantId",  // used in cross-tenant write log

        // ── Auth / security metadata (enum values, not personal data) ───────────
        "Jti",           "jti",
        "Role",          "role",
        "UserRole",      "user_role",
        "RequiredRole",  "required_role",
        "TenantRole",    "tenant_role",
        "HouseholdRole", "household_role",
        "Reason",        "reason",
        "EventType",     "event_type",
        "Severity",      "severity",
        "EntityType",    "entity_type",
        "Count",         "count",
        "Method",        "method",
        "Path",          "path",

        // ── ILogger template metadata ─────────────────────────────────────────
        "{OriginalFormat}",
        "CategoryName",

        // ── OTel HTTP semantic conventions ─────────────────────────────────────
        "http.method",         "http.status_code",    "http.route",
        "http.url",            "http.target",         "http.host",
        "http.scheme",         "http.flavor",         "http.user_agent",
        "http.request_content_length",               "http.response_content_length",
        // OTel 1.20+ / Stable HTTP semconv
        "http.request.method", "http.response.status_code", "url.scheme",
        "url.path",            "url.query",           "server.address", "server.port",

        // ── OTel DB semantic conventions (operation metadata, NOT statement) ───
        "db.system",      "db.name",         "db.operation",
        "db.sql.table",

        // ── OTel exception attributes ──────────────────────────────────────────
        "exception.type", "exception.message", "exception.stacktrace",

        // ── OTel network / infrastructure ─────────────────────────────────────
        "net.transport",      "net.host.name",     "net.host.port",
        "net.peer.name",      "net.peer.port",
        "server.socket.address", "server.socket.port",

        // ── OTel service resource ──────────────────────────────────────────────
        "service.name",   "service.version",   "service.instance.id",

        // ── OTel code / telemetry metadata ────────────────────────────────────
        "code.function",  "code.namespace",    "code.filepath", "code.lineno",
        "otel.status_code", "otel.status_description",
        "otel.library.name", "otel.library.version",
    };

    public override void OnEnd(LogRecord logRecord)
    {
        var attributes = logRecord.Attributes;
        if (attributes is null || attributes.Count == 0)
            return;

        List<KeyValuePair<string, object?>>? redacted = null;

        for (var i = 0; i < attributes.Count; i++)
        {
            var attr = attributes[i];
            if (!AllowedKeys.Contains(attr.Key))
            {
                // Lazily allocate only when there is something to redact.
                if (redacted is null)
                {
                    redacted = new List<KeyValuePair<string, object?>>(attributes.Count);
                    // Copy all attributes processed so far (they were clean).
                    for (var j = 0; j < i; j++)
                        redacted.Add(attributes[j]);
                }
                redacted.Add(new KeyValuePair<string, object?>(attr.Key, RedactedValue));
            }
            else
            {
                redacted?.Add(attr);
            }
        }

        if (redacted is not null)
        {
            logRecord.Attributes = redacted;
            // Clear the pre-formatted message to avoid the raw values leaking
            // through the expanded string. The message template in {OriginalFormat}
            // is preserved for shape-of-log debuggability.
            logRecord.FormattedMessage = null;
        }
    }

    public static bool IsAllowed(string key) => AllowedKeys.Contains(key);
}
