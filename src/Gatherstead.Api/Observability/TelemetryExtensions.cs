using Azure.Identity;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Gatherstead.Api.Observability;

public static class TelemetryExtensions
{
    /// <summary>
    /// Registers Azure Monitor OpenTelemetry with managed-identity auth, the application's
    /// custom ActivitySource and Meter, and SqlClient auto-instrumentation.
    /// Reads APPLICATIONINSIGHTS_CONNECTION_STRING from environment automatically.
    /// </summary>
    public static IServiceCollection AddGathersteadTelemetry(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Skip in development when no connection string is configured — avoids startup noise.
        var connectionString = configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
        if (string.IsNullOrEmpty(connectionString))
            return services;

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(GathersteadTelemetry.ServiceName))
            .UseAzureMonitor(options =>
            {
                // DefaultAzureCredential picks up the user-assigned managed identity via
                // AZURE_CLIENT_ID, matching the pattern used for SQL and Key Vault auth.
                options.Credential = new DefaultAzureCredential();
            })
            .WithTracing(tracing => tracing
                .AddSource(GathersteadTelemetry.SourceName)
                .AddProcessor(new PiiRedactionActivityProcessor()))
            .WithMetrics(metrics => metrics
                .AddMeter(GathersteadTelemetry.SourceName))
            .WithLogging(logging => logging
                .AddProcessor(new PiiRedactionLogProcessor()));

        return services;
    }
}
