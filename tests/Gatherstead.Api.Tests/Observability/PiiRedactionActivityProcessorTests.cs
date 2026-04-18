using Gatherstead.Api.Observability;
using System.Diagnostics;

namespace Gatherstead.Api.Tests.Observability;

/// <summary>
/// Verifies that PiiRedactionActivityProcessor redacts non-allowlisted Activity tags
/// and always strips db.statement / db.query.text regardless of allowlist status.
/// </summary>
public sealed class PiiRedactionActivityProcessorTests : IDisposable
{
    private static readonly ActivitySource Source = new("Gatherstead.Test.PiiRedaction");

    private readonly ActivityListener _listener;

    public PiiRedactionActivityProcessorTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose() => _listener.Dispose();

    private static Activity StartActivity(string name = "TestOperation")
    {
        var activity = Source.StartActivity(name);
        Assert.NotNull(activity);
        return activity;
    }

    // ── safe tags ────────────────────────────────────────────────────────────────

    [Fact]
    public void AllowlistedTags_AreNotRedacted()
    {
        using var activity = StartActivity();
        var tenantId = Guid.NewGuid().ToString();
        activity.SetTag("tenant.id", tenantId);
        activity.SetTag("user.id", Guid.NewGuid().ToString());
        activity.SetTag("http.method", "GET");
        activity.SetTag("http.status_code", "200");

        new PiiRedactionActivityProcessor().OnEnd(activity);

        Assert.Equal(tenantId, activity.GetTagItem("tenant.id") as string);
        Assert.NotEqual(PiiRedactionLogProcessor.RedactedValue, activity.GetTagItem("user.id") as string);
        Assert.Equal("GET", activity.GetTagItem("http.method") as string);
        Assert.Equal("200", activity.GetTagItem("http.status_code") as string);
    }

    // ── PII tags ─────────────────────────────────────────────────────────────────

    [Fact]
    public void UnknownTag_IsRedacted()
    {
        using var activity = StartActivity();
        activity.SetTag("CustomerEmail", "alice@example.com");

        new PiiRedactionActivityProcessor().OnEnd(activity);

        Assert.Equal(PiiRedactionLogProcessor.RedactedValue, activity.GetTagItem("CustomerEmail") as string);
    }

    [Fact]
    public void DbStatement_IsAlwaysRedacted()
    {
        using var activity = StartActivity();
        activity.SetTag("db.operation", "SELECT");  // safe — should survive
        activity.SetTag("db.statement", "SELECT * FROM Users WHERE Email = 'alice@example.com'");

        new PiiRedactionActivityProcessor().OnEnd(activity);

        Assert.Equal("SELECT", activity.GetTagItem("db.operation") as string);
        Assert.Equal(PiiRedactionLogProcessor.RedactedValue, activity.GetTagItem("db.statement") as string);
    }

    [Fact]
    public void DbQueryText_IsAlwaysRedacted()
    {
        using var activity = StartActivity();
        activity.SetTag("db.query.text", "UPDATE Members SET Name = @name WHERE Id = @id");

        new PiiRedactionActivityProcessor().OnEnd(activity);

        Assert.Equal(PiiRedactionLogProcessor.RedactedValue, activity.GetTagItem("db.query.text") as string);
    }

    // ── mixed scenario ───────────────────────────────────────────────────────────

    [Fact]
    public void MixedTags_OnlyPiiAndDbStatementRedacted()
    {
        using var activity = StartActivity();
        activity.SetTag("tenant.id", "guid-abc");
        activity.SetTag("user.id", "guid-xyz");
        activity.SetTag("http.method", "POST");
        activity.SetTag("db.system", "mssql");
        activity.SetTag("db.statement", "EXEC sp_GetUser @email='alice@example.com'");
        activity.SetTag("MemberName", "Alice Smith");   // PII

        new PiiRedactionActivityProcessor().OnEnd(activity);

        Assert.Equal("guid-abc", activity.GetTagItem("tenant.id") as string);
        Assert.Equal("guid-xyz", activity.GetTagItem("user.id") as string);
        Assert.Equal("POST", activity.GetTagItem("http.method") as string);
        Assert.Equal("mssql", activity.GetTagItem("db.system") as string);
        Assert.Equal(PiiRedactionLogProcessor.RedactedValue, activity.GetTagItem("db.statement") as string);
        Assert.Equal(PiiRedactionLogProcessor.RedactedValue, activity.GetTagItem("MemberName") as string);
    }

    [Fact]
    public void NoTags_ProcessorDoesNotThrow()
    {
        using var activity = StartActivity();
        // activity has no tags

        var ex = Record.Exception(() => new PiiRedactionActivityProcessor().OnEnd(activity));

        Assert.Null(ex);
    }
}
