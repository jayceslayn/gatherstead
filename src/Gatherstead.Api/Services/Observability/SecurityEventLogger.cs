using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Gatherstead.Api.Observability;
using Gatherstead.Data;
using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Services.Observability;

public sealed class SecurityEventLogger : ISecurityEventLogger
{
    private readonly GathersteadDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<SecurityEventLogger> _logger;

    public SecurityEventLogger(
        GathersteadDbContext dbContext,
        IHttpContextAccessor httpContextAccessor,
        ILogger<SecurityEventLogger> logger)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task LogAsync(
        SecurityEventType eventType,
        SecurityEventSeverity severity,
        string resource = "",
        string? detail = null,
        Guid? tenantId = null,
        Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        var correlationId = Activity.Current?.TraceId.ToString() ?? "";

        var eventTypeName = eventType.ToString();
        var severityName = severity.ToString();

        GathersteadMetrics.RecordSecurityEvent(eventTypeName, severityName);

        Activity.Current?.AddEvent(new ActivityEvent("security_event",
            tags: new ActivityTagsCollection
            {
                { "event_type", eventTypeName },
                { "severity", severityName },
                { "tenant.id", tenantId?.ToString() },
                { "user.id", userId?.ToString() },
                { "resource", resource },
            }));

        try
        {
            _dbContext.SecurityEvents.Add(new SecurityEvent
            {
                Id = Guid.NewGuid(),
                OccurredAt = DateTime.UtcNow,
                EventType = eventType,
                Severity = severity,
                TenantId = tenantId,
                UserId = userId,
                CorrelationId = correlationId,
                IpAddressHash = HashValue(GetIpAddress()),
                UserAgentHash = HashValue(GetUserAgent()),
                Resource = resource,
                Detail = detail,
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to persist {EventType} security event for tenant {TenantId} user {UserId}",
                eventType, tenantId, userId);
        }
    }

    private string? GetIpAddress() =>
        _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    private string? GetUserAgent() =>
        _httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString();

    private static byte[]? HashValue(string? value)
    {
        if (value is null) return null;
        // Daily salt prevents cross-day correlation while enabling same-day grouping.
        var salted = $"{value}|{DateTime.UtcNow:yyyy-MM-dd}";
        return SHA256.HashData(Encoding.UTF8.GetBytes(salted));
    }
}
