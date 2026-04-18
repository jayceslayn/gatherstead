using System.Diagnostics;
using Gatherstead.Data;

namespace Gatherstead.Api.Middleware;

/// <summary>
/// Emits X-Correlation-Id on every response and enriches the OTel Activity with
/// tenant.id and user.id after authentication has run.
/// </summary>
public class CorrelationEnrichmentMiddleware
{
    private readonly RequestDelegate _next;

    public CorrelationEnrichmentMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ICurrentUserContext userContext,
        ICurrentTenantContext tenantContext)
    {
        var activity = Activity.Current;

        // Set the correlation header before the response body is written so callers
        // can reference it even when the handler returns an error response.
        var correlationId = activity?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-Correlation-Id"] = correlationId;
            return Task.CompletedTask;
        });

        await _next(context);

        // Enrich after the handler runs — auth has completed and contexts are populated.
        if (activity is null)
            return;

        var tenantId = tenantContext.TenantId;
        if (tenantId.HasValue)
            activity.SetTag("tenant.id", tenantId.Value.ToString());

        // Only read UserId when the request is authenticated to avoid spurious DB queries.
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userId = userContext.UserId;
            if (userId.HasValue)
                activity.SetTag("user.id", userId.Value.ToString());
        }
    }
}
