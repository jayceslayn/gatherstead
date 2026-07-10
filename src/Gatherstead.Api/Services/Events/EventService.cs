using Gatherstead.Api.Contracts.Attributes;
using Gatherstead.Api.Contracts.Events;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Services.Attributes;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.Planning;
using Gatherstead.Api.Services.Validation;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Api.Services.Events;

public class EventService : IEventService
{
    private const string EntityDisplayName = "Event";

    private readonly GathersteadDbContext _dbContext;
    private readonly ICurrentTenantContext _currentTenantContext;
    private readonly IMemberAuthorizationService _memberAuthorizationService;
    private readonly PlanSyncService _planSyncService;
    private readonly IAuditVisibilityContext _auditVisibility;

    public EventService(
        GathersteadDbContext dbContext,
        ICurrentTenantContext currentTenantContext,
        IMemberAuthorizationService memberAuthorizationService,
        PlanSyncService planSyncService,
        IAuditVisibilityContext auditVisibility)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentTenantContext = currentTenantContext ?? throw new ArgumentNullException(nameof(currentTenantContext));
        _memberAuthorizationService = memberAuthorizationService ?? throw new ArgumentNullException(nameof(memberAuthorizationService));
        _planSyncService = planSyncService ?? throw new ArgumentNullException(nameof(planSyncService));
        _auditVisibility = auditVisibility ?? throw new ArgumentNullException(nameof(auditVisibility));
    }

    public async Task<BaseEntityResponse<IReadOnlyCollection<EventDto>>> ListAsync(
        Guid tenantId,
        IEnumerable<Guid>? ids = null,
        CancellationToken cancellationToken = default)
    {
        var response = new BaseEntityResponse<IReadOnlyCollection<EventDto>>();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        // Attributes ride along on lists (mirrors GetAsync) so list-sourced edits don't wipe them.
        // A caller with no tenant role sees none, so skip loading them entirely.
        var callerRole = await _memberAuthorizationService.GetCallerTenantRoleAsync(tenantId, cancellationToken);

        var query = _dbContext.Events
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId);
        if (callerRole is not null)
            query = query.Include(e => e.Attributes);

        if (ids is not null)
        {
            var idList = ids.ToList();
            if (idList.Count > 0)
                query = query.Where(e => idList.Contains(e.Id));
        }

        var events = await query.ToListAsync(cancellationToken);

        return BaseEntityResponse<IReadOnlyCollection<EventDto>>.SuccessfulResponse(
            events.Select(e => MapToDto(e, AttributeVisibilityHelper.Visible(e.Attributes, callerRole))).ToList());
    }

    public async Task<EventResponse> GetAsync(
        Guid tenantId,
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        var response = new EventResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        var @event = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.Events.AsNoTracking()
                .Include(e => e.Attributes)
                .Where(e => e.TenantId == tenantId && e.Id == eventId),
            EntityDisplayName,
            cancellationToken);

        if (@event is null) return response;

        var callerRole = await _memberAuthorizationService.GetCallerTenantRoleAsync(tenantId, cancellationToken);
        response.SetSuccess(MapToDto(@event, AttributeVisibilityHelper.Visible(@event.Attributes, callerRole)));
        return response;
    }

    public async Task<EventResponse> CreateAsync(
        Guid tenantId,
        CreateEventRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new EventResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!ServiceGuards.RequireRequest(request, "create event", response))
            return response;
        if (!await ServiceGuards.AuthorizeEventManageAsync(response, _memberAuthorizationService, tenantId, cancellationToken))
            return response;

        ServiceValidationHelper.TryNormalizeString(request.Name, "Event name", response, out string normalizedName);
        if (request.EndDate < request.StartDate)
            response.AddResponseMessage(MessageType.ERROR, "End date must be on or after start date.");
        if (ServiceValidationHelper.HasErrors(response))
            return response;

        var @event = new Event
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PropertyId = request.PropertyId,
            Name = normalizedName,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Notes = request.Notes?.Trim(),
        };

        _dbContext.Events.Add(@event);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var callerRole = await _memberAuthorizationService.GetCallerTenantRoleAsync(tenantId, cancellationToken);
        List<AttributeDto> attrs = [];

        if (request.Attributes is { Count: > 0 })
        {
            await AttributeSyncHelper.SyncAsync(
                _dbContext.EventAttributes.Where(a => a.EventId == @event.Id),
                _dbContext.EventAttributes,
                request.Attributes,
                a => AttributeVisibilityHelper.IsVisible(a, callerRole),
                tenantId,
                () => new EventAttribute { TenantId = tenantId, EventId = @event.Id },
                applyExtra: null,
                cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var savedAttrs = await _dbContext.EventAttributes.AsNoTracking()
                .Where(a => a.EventId == @event.Id).ToListAsync(cancellationToken);
            attrs = AttributeVisibilityHelper.Visible(savedAttrs, callerRole);
        }

        response.SetSuccess(MapToDto(@event, attrs));
        return response;
    }

    public async Task<EventResponse> UpdateAsync(
        Guid tenantId,
        Guid eventId,
        UpdateEventRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new EventResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!ServiceGuards.RequireRequest(request, "update event", response))
            return response;
        if (!await ServiceGuards.AuthorizeEventManageAsync(response, _memberAuthorizationService, tenantId, cancellationToken))
            return response;

        ServiceValidationHelper.TryNormalizeString(request.Name, "Event name", response, out string normalizedName);
        if (request.EndDate < request.StartDate)
            response.AddResponseMessage(MessageType.ERROR, "End date must be on or after start date.");
        if (ServiceValidationHelper.HasErrors(response))
            return response;

        var @event = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.Events.Where(e => e.TenantId == tenantId && e.Id == eventId),
            EntityDisplayName,
            cancellationToken);

        if (@event is null) return response;

        var datesChanged = request.StartDate != @event.StartDate || request.EndDate != @event.EndDate;

        @event.Name = normalizedName;
        @event.StartDate = request.StartDate;
        @event.EndDate = request.EndDate;
        @event.Notes = request.Notes?.Trim();

        if (datesChanged)
            await _planSyncService.SyncEventPlansAsync(tenantId, @event, cancellationToken);

        var callerRole = await _memberAuthorizationService.GetCallerTenantRoleAsync(tenantId, cancellationToken);

        if (request.Attributes is not null)
        {
            await AttributeSyncHelper.SyncAsync(
                _dbContext.EventAttributes.Where(a => a.EventId == eventId),
                _dbContext.EventAttributes,
                request.Attributes,
                a => AttributeVisibilityHelper.IsVisible(a, callerRole),
                tenantId,
                () => new EventAttribute { TenantId = tenantId, EventId = eventId },
                applyExtra: null,
                cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var savedAttrs = await _dbContext.EventAttributes.AsNoTracking()
            .Where(a => a.EventId == eventId).ToListAsync(cancellationToken);
        response.SetSuccess(MapToDto(@event, AttributeVisibilityHelper.Visible(savedAttrs, callerRole)));
        return response;
    }

    public async Task<EventResponse> DeleteAsync(
        Guid tenantId,
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        var response = new EventResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!await ServiceGuards.AuthorizeEventManageAsync(response, _memberAuthorizationService, tenantId, cancellationToken))
            return response;

        var @event = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.Events.Where(e => e.TenantId == tenantId && e.Id == eventId),
            EntityDisplayName,
            cancellationToken);

        if (@event is null) return response;

        if (@event.IsDeleted)
        {
            response.AddResponseMessage(MessageType.WARNING, $"{EntityDisplayName} already deleted.");
            return response;
        }

        @event.IsDeleted = true;

        var childAttrs = await _dbContext.EventAttributes
            .Where(a => a.EventId == eventId).ToListAsync(cancellationToken);
        foreach (var attr in childAttrs)
            attr.IsDeleted = true;

        await _dbContext.SaveChangesAsync(cancellationToken);

        response.SetSuccess(MapToDto(@event, []));
        return response;
    }

    public async Task<EventResponse> SyncPlansAsync(
        Guid tenantId,
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        var response = new EventResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!await ServiceGuards.AuthorizeEventManageAsync(response, _memberAuthorizationService, tenantId, cancellationToken))
            return response;

        var @event = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.Events.Where(e => e.TenantId == tenantId && e.Id == eventId),
            EntityDisplayName,
            cancellationToken);

        if (@event is null) return response;

        await _planSyncService.SyncEventPlansAsync(tenantId, @event, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        response.SetSuccess(MapToDto(@event, []));
        return response;
    }

    private EventDto MapToDto(Event @event, IReadOnlyList<AttributeDto> attributes) => new(
        @event.Id,
        @event.TenantId,
        @event.PropertyId,
        @event.Name,
        @event.StartDate,
        @event.EndDate,
        @event.Notes,
        attributes,
        @event.ToAuditInfo(_auditVisibility.IncludeAudit));
}
