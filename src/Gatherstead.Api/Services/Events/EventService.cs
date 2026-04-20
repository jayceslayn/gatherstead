using Gatherstead.Api.Contracts.Events;
using Gatherstead.Api.Contracts.Responses;
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

    public EventService(
        GathersteadDbContext dbContext,
        ICurrentTenantContext currentTenantContext,
        IMemberAuthorizationService memberAuthorizationService,
        PlanSyncService planSyncService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentTenantContext = currentTenantContext ?? throw new ArgumentNullException(nameof(currentTenantContext));
        _memberAuthorizationService = memberAuthorizationService ?? throw new ArgumentNullException(nameof(memberAuthorizationService));
        _planSyncService = planSyncService ?? throw new ArgumentNullException(nameof(planSyncService));
    }

    public async Task<BaseEntityResponse<IReadOnlyCollection<EventDto>>> ListAsync(
        Guid tenantId,
        IEnumerable<Guid>? ids = null,
        CancellationToken cancellationToken = default)
    {
        var response = new BaseEntityResponse<IReadOnlyCollection<EventDto>>();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        var query = _dbContext.Events
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId);

        if (ids is not null)
        {
            var idList = ids.ToList();
            if (idList.Count > 0)
                query = query.Where(e => idList.Contains(e.Id));
        }

        var events = await query
            .Select(e => MapToDto(e))
            .ToListAsync(cancellationToken);

        return BaseEntityResponse<IReadOnlyCollection<EventDto>>.SuccessfulResponse(events);
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
            _dbContext.Events.AsNoTracking().Where(e => e.TenantId == tenantId && e.Id == eventId),
            EntityDisplayName,
            cancellationToken);

        if (@event is null) return response;

        response.SetSuccess(MapToDto(@event));
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
        if (!await ServiceGuards.AuthorizeTenantManageAsync(response, _memberAuthorizationService, tenantId, cancellationToken))
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
        };

        _dbContext.Events.Add(@event);
        await _dbContext.SaveChangesAsync(cancellationToken);

        response.SetSuccess(MapToDto(@event));
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
        if (!await ServiceGuards.AuthorizeTenantManageAsync(response, _memberAuthorizationService, tenantId, cancellationToken))
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

        if (datesChanged)
            await _planSyncService.SyncEventPlansAsync(tenantId, @event, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        response.SetSuccess(MapToDto(@event));
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
        if (!await ServiceGuards.AuthorizeTenantManageAsync(response, _memberAuthorizationService, tenantId, cancellationToken))
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
        await _dbContext.SaveChangesAsync(cancellationToken);

        response.SetSuccess(MapToDto(@event));
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
        if (!await ServiceGuards.AuthorizeTenantManageAsync(response, _memberAuthorizationService, tenantId, cancellationToken))
            return response;

        var @event = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.Events.Where(e => e.TenantId == tenantId && e.Id == eventId),
            EntityDisplayName,
            cancellationToken);

        if (@event is null) return response;

        await _planSyncService.SyncEventPlansAsync(tenantId, @event, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        response.SetSuccess(MapToDto(@event));
        return response;
    }

    private static EventDto MapToDto(Event @event) => new(
        @event.Id,
        @event.TenantId,
        @event.PropertyId,
        @event.Name,
        @event.StartDate,
        @event.EndDate,
        @event.CreatedAt,
        @event.UpdatedAt,
        @event.IsDeleted,
        @event.DeletedAt,
        @event.DeletedByUserId);
}
