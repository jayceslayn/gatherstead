using Gatherstead.Api.Observability;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace Gatherstead.Api.Tests.Observability;

/// <summary>
/// Verifies that PiiRedactionLogProcessor redacts non-allowlisted log attributes
/// before export and clears FormattedMessage when redaction occurs.
/// </summary>
public sealed class PiiRedactionLogProcessorTests
{
    // ── helpers ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a LoggerFactory that runs PiiRedactionLogProcessor before a simple
    /// collecting exporter, then returns the captured LogRecords.
    /// </summary>
    private static (ILoggerFactory Factory, List<CapturedLog> Captured) BuildFactory()
    {
        var captured = new List<CapturedLog>();

        var factory = LoggerFactory.Create(builder =>
            builder
                .SetMinimumLevel(LogLevel.Trace)
                .AddOpenTelemetry(otel =>
                {
                    otel.IncludeFormattedMessage = true;
                    // PiiRedaction runs first, then Recording captures the result.
                    otel.AddProcessor(new PiiRedactionLogProcessor());
                    otel.AddProcessor(new RecordingLogProcessor(record =>
                        captured.Add(new CapturedLog(
                            record.Attributes?.ToList() ?? [],
                            record.FormattedMessage))));
                }));

        return (factory, captured);
    }

    // ── tests ────────────────────────────────────────────────────────────────────

    [Fact]
    public void AllowlistedAttributes_AreNotRedacted()
    {
        var (factory, captured) = BuildFactory();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        using (factory)
            factory.CreateLogger<PiiRedactionLogProcessorTests>()
                .LogWarning("Auth denied for tenant {TenantId} user {UserId}", tenantId, userId);

        Assert.Single(captured);
        var attrs = captured[0].Attributes;

        Assert.True(attrs.Any(a => a.Key == "TenantId" && a.Value?.ToString() != PiiRedactionLogProcessor.RedactedValue),
            "TenantId should not be redacted");
        Assert.True(attrs.Any(a => a.Key == "UserId" && a.Value?.ToString() != PiiRedactionLogProcessor.RedactedValue),
            "UserId should not be redacted");
    }

    [Fact]
    public void PiiAttribute_Email_IsRedacted()
    {
        var (factory, captured) = BuildFactory();
        using (factory)
        {
            factory.CreateLogger<PiiRedactionLogProcessorTests>()
                .LogWarning("Login attempt for {Email}", "user@example.com");
        }

        var attrs = captured[0].Attributes;
        Assert.Contains(attrs, a => a.Key == "Email" && (string?)a.Value == PiiRedactionLogProcessor.RedactedValue);
    }

    [Fact]
    public void PiiAttribute_BirthDate_IsRedacted()
    {
        var (factory, captured) = BuildFactory();
        using (factory)
        {
            factory.CreateLogger<PiiRedactionLogProcessorTests>()
                .LogInformation("Member profile: {BirthDate}", "1985-06-15");
        }

        var attrs = captured[0].Attributes;
        Assert.Contains(attrs, a => a.Key == "BirthDate" && (string?)a.Value == PiiRedactionLogProcessor.RedactedValue);
    }

    [Fact]
    public void PiiAttribute_DietaryNote_IsRedacted()
    {
        var (factory, captured) = BuildFactory();
        using (factory)
        {
            factory.CreateLogger<PiiRedactionLogProcessorTests>()
                .LogInformation("Dietary note: {DietaryNote}", "severe peanut allergy");
        }

        var attrs = captured[0].Attributes;
        Assert.Contains(attrs, a => a.Key == "DietaryNote" && (string?)a.Value == PiiRedactionLogProcessor.RedactedValue);
    }

    [Fact]
    public void MixedAttributes_OnlyPiiIsRedacted()
    {
        var (factory, captured) = BuildFactory();
        using (factory)
        {
            factory.CreateLogger<PiiRedactionLogProcessorTests>()
                .LogWarning("Event {EventType} for {TenantId} contact {Email}",
                    "AuthFailure", Guid.NewGuid(), "admin@example.com");
        }

        var attrs = captured[0].Attributes;

        Assert.Contains(attrs, a => a.Key == "EventType" && a.Value?.ToString() != PiiRedactionLogProcessor.RedactedValue);
        Assert.Contains(attrs, a => a.Key == "TenantId" && a.Value?.ToString() != PiiRedactionLogProcessor.RedactedValue);
        Assert.Contains(attrs, a => a.Key == "Email" && a.Value?.ToString() == PiiRedactionLogProcessor.RedactedValue);
    }

    [Fact]
    public void FormattedMessage_IsClearedWhenRedactionOccurs()
    {
        var (factory, captured) = BuildFactory();
        using (factory)
        {
            factory.CreateLogger<PiiRedactionLogProcessorTests>()
                .LogWarning("Greeting {Name}", "Alice");
        }

        // FormattedMessage would have been "Greeting Alice" — must be cleared.
        Assert.Null(captured[0].FormattedMessage);
    }

    [Fact]
    public void FormattedMessage_IsPreservedWhenNoRedactionOccurs()
    {
        var (factory, captured) = BuildFactory();
        using (factory)
        {
            var tenantId = Guid.NewGuid();
            factory.CreateLogger<PiiRedactionLogProcessorTests>()
                .LogWarning("Tenant {TenantId} denied", tenantId);
        }

        // All attributes are on the allowlist — FormattedMessage should be intact.
        Assert.NotNull(captured[0].FormattedMessage);
    }

    [Fact]
    public void OriginalFormat_IsAlwaysPreserved()
    {
        var (factory, captured) = BuildFactory();
        using (factory)
        {
            factory.CreateLogger<PiiRedactionLogProcessorTests>()
                .LogWarning("Login from {Email}", "test@example.com");
        }

        var attrs = captured[0].Attributes;
        Assert.Contains(attrs, a => a.Key == "{OriginalFormat}");
    }

    [Fact]
    public void NoAttributes_ProcessorDoesNotThrow()
    {
        var (factory, captured) = BuildFactory();
        using (factory)
        {
            factory.CreateLogger<PiiRedactionLogProcessorTests>()
                .LogInformation("Plain message with no structured properties");
        }

        Assert.Single(captured);
    }

    // ── inner types ──────────────────────────────────────────────────────────────

    private sealed record CapturedLog(
        List<KeyValuePair<string, object?>> Attributes,
        string? FormattedMessage);

    // Calls onRecord synchronously in OnEnd — runs after all preceding processors
    // (including PiiRedactionLogProcessor) have had a chance to mutate the record.
    private sealed class RecordingLogProcessor(Action<LogRecord> onRecord) : BaseProcessor<LogRecord>
    {
        public override void OnEnd(LogRecord record) => onRecord(record);
    }
}
