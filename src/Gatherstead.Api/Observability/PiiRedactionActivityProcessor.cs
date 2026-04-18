using OpenTelemetry;
using System.Diagnostics;

namespace Gatherstead.Api.Observability;

/// <summary>
/// OTel span processor that redacts Activity tags not in the allowlist and always
/// removes <c>db.statement</c> / <c>db.query.text</c> (SQL text that can contain
/// parameter values) before export.
///
/// Tag allowlist is shared with <see cref="PiiRedactionLogProcessor"/> so the two
/// policies stay in sync. See docs/OBSERVABILITY.md for the logging contract.
/// </summary>
public sealed class PiiRedactionActivityProcessor : BaseProcessor<Activity>
{
    // db.statement and db.query.text can include parameter values injected at runtime.
    // Always strip them regardless of allowlist membership.
    private static readonly HashSet<string> AlwaysRedactKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "db.statement",
        "db.query.text",
        "db.query",
    };

    public override void OnEnd(Activity activity)
    {
        // Materialise the tag list first to avoid mutating an enumerator.
        var tagsToProcess = activity.TagObjects.ToList();
        if (tagsToProcess.Count == 0)
            return;

        foreach (var (key, _) in tagsToProcess)
        {
            if (AlwaysRedactKeys.Contains(key) || !PiiRedactionLogProcessor.IsAllowed(key))
                activity.SetTag(key, PiiRedactionLogProcessor.RedactedValue);
        }
    }
}
