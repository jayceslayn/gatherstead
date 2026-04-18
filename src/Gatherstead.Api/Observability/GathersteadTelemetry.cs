using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Gatherstead.Api.Observability;

/// <summary>
/// Central registry for the application's ActivitySource and Meter.
/// Consume these singletons from services rather than creating new instances.
/// </summary>
public static class GathersteadTelemetry
{
    public const string ServiceName = "gatherstead-api";
    public const string SourceName = "Gatherstead.Api";

    public static readonly ActivitySource ActivitySource = new(SourceName);
    public static readonly Meter Meter = new(SourceName);
}
