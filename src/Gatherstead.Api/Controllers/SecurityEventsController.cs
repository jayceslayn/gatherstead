using Gatherstead.Api.Security;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Api.Controllers;

[ApiController]
[Authorize]
public class SecurityEventsController : ControllerBase
{
    private readonly GathersteadDbContext _dbContext;

    public SecurityEventsController(GathersteadDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>Returns security events for a specific tenant. Requires Manager or Owner role.</summary>
    [HttpGet("api/tenants/{tenantId:guid}/security-events")]
    [RequireTenantAccess(TenantRole.Manager)]
    public async Task<ActionResult<SecurityEventPageDto>> GetTenantSecurityEvents(
        Guid tenantId,
        [FromQuery] SecurityEventType? eventType = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        [FromQuery] int page = 0,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _dbContext.SecurityEvents
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId);

        query = ApplyFilters(query, eventType, dateFrom, dateTo);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(e => e.OccurredAt)
            .Skip(page * pageSize)
            .Take(pageSize)
            .Select(e => MapToDto(e))
            .ToListAsync(cancellationToken);

        return Ok(new SecurityEventPageDto(total, page, pageSize, items));
    }

    /// <summary>Returns security events across all tenants. Requires App Admin role.</summary>
    [HttpGet("api/admin/security-events")]
    [RequireAppAdmin]
    public async Task<ActionResult<SecurityEventPageDto>> GetAllSecurityEvents(
        [FromQuery] SecurityEventType? eventType = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        [FromQuery] Guid? tenantId = null,
        [FromQuery] int page = 0,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _dbContext.SecurityEvents.AsNoTracking();

        if (tenantId.HasValue)
            query = query.Where(e => e.TenantId == tenantId);

        query = ApplyFilters(query, eventType, dateFrom, dateTo);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(e => e.OccurredAt)
            .Skip(page * pageSize)
            .Take(pageSize)
            .Select(e => MapToDto(e))
            .ToListAsync(cancellationToken);

        return Ok(new SecurityEventPageDto(total, page, pageSize, items));
    }

    private static IQueryable<SecurityEvent> ApplyFilters(
        IQueryable<SecurityEvent> query,
        SecurityEventType? eventType,
        DateTime? dateFrom,
        DateTime? dateTo)
    {
        if (eventType.HasValue)
            query = query.Where(e => e.EventType == eventType.Value);
        if (dateFrom.HasValue)
            query = query.Where(e => e.OccurredAt >= dateFrom.Value);
        if (dateTo.HasValue)
            query = query.Where(e => e.OccurredAt <= dateTo.Value);
        return query;
    }

    private static SecurityEventDto MapToDto(SecurityEvent e) =>
        new(e.Id, e.OccurredAt, e.EventType.ToString(), e.Severity.ToString(),
            e.TenantId, e.UserId, e.CorrelationId, e.Resource, e.Detail);
}

public sealed record SecurityEventDto(
    Guid Id,
    DateTime OccurredAt,
    string EventType,
    string Severity,
    Guid? TenantId,
    Guid? UserId,
    string CorrelationId,
    string Resource,
    string? Detail);

public sealed record SecurityEventPageDto(
    int Total,
    int Page,
    int PageSize,
    IReadOnlyList<SecurityEventDto> Items);
