using System.Diagnostics;
using Gatherstead.Api.Services.Observability;
using Gatherstead.Data;
using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Middleware;

public class ExceptionLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionLoggingMiddleware> _logger;

    public ExceptionLoggingMiddleware(RequestDelegate next, ILogger<ExceptionLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var activity = Activity.Current;
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            var correlationId = activity?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");

            _logger.LogError(
                ex,
                "Unhandled exception on {Method} {Path}. CorrelationId: {CorrelationId}",
                context.Request.Method,
                context.Request.Path,
                correlationId);

            if (ex is CrossTenantWriteBlockedException ctwb)
                await LogCrossTenantWriteBlockedAsync(context, ctwb);

            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "application/problem+json";
                context.Response.Headers["X-Correlation-Id"] = correlationId;

                await context.Response.WriteAsJsonAsync(new
                {
                    type = "https://tools.ietf.org/html/rfc9110#section-15.6.1",
                    title = "An unexpected error occurred.",
                    status = 500,
                    correlationId
                });
            }
        }
    }

    /// <summary>
    /// Records a <see cref="SecurityEventType.CrossTenantWriteBlocked"/> event from a FRESH DI scope.
    /// The request-scoped <c>GathersteadDbContext</c> still has the poisoned cross-tenant entity in its
    /// change tracker, so saving through it would re-trigger the interceptor and lose the event. A fresh
    /// scope gets a clean context; its tenant/user context still resolve from the current HttpContext, so
    /// the event's TenantId matches the current context and passes validation.
    /// </summary>
    private async Task LogCrossTenantWriteBlockedAsync(HttpContext context, CrossTenantWriteBlockedException ex)
    {
        try
        {
            var userId = context.RequestServices.GetService<ICurrentUserContext>()?.UserId;

            var scopeFactory = context.RequestServices.GetRequiredService<IServiceScopeFactory>();
            await using var scope = scopeFactory.CreateAsyncScope();
            var logger = scope.ServiceProvider.GetRequiredService<ISecurityEventLogger>();

            await logger.LogAsync(
                SecurityEventType.CrossTenantWriteBlocked,
                SecurityEventSeverity.Critical,
                resource: ex.EntityType,
                detail: $"{{\"reason\":\"{ex.Reason}\",\"entityTenantId\":\"{ex.EntityTenantId}\"}}",
                tenantId: ex.CurrentTenantId,
                userId: userId,
                cancellationToken: context.RequestAborted);
        }
        catch (Exception logEx)
        {
            // Never let security logging mask the original failure or the 500 response.
            _logger.LogError(logEx, "Failed to record CrossTenantWriteBlocked security event.");
        }
    }
}
