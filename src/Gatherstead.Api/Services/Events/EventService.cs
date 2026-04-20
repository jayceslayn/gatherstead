using Gatherstead.Api.Contracts.Events;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Services.Validation;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Gatherstead.Data.Planning;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Api.Services.Events;

public class EventService : IEventService
{
    private readonly GathersteadDbContext _dbContext;
    private readonly ICurrentTenantContext _currentTenantContext;

    public EventService(
        GathersteadDbContext dbContext,
        ICurrentTenantContext currentTenantContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentTenantContext = currentTenantContext ?? throw new ArgumentNullException(nameof(currentTenantContext));
    }

    public async Task<BaseEntityResponse<IReadOnlyCollection<EventDto>>> ListAsync(
        Guid tenantId,
        IEnumerable<Guid>? ids = null,
        CancellationToken cancellationToken = default)
    {
        var response = new BaseEntityResponse<IReadOnlyCollection<EventDto>>();
        ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response);

        if (ServiceValidationHelper.HasErrors(response))
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
        ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response);

        if (ServiceValidationHelper.HasErrors(response))
            return response;

        var @event = await _dbContext.Events
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.Id == eventId)
            .SingleOrDefaultAsync(cancellationToken);

        if (@event is null)
        {
            response.AddResponseMessage(MessageType.ERROR, "Event not found.");
            return response;
        }

        response.SetSuccess(MapToDto(@event));
        return response;
    }

    public async Task<EventResponse> CreateAsync(
        Guid tenantId,
        CreateEventRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new EventResponse();
        ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response);

        if (request is null)
        {
            response.AddResponseMessage(MessageType.ERROR, "A create event request is required.");
            return response;
        }

        ServiceValidationHelper.TryNormalizeString(request.Name, "Event name", response, out string normalizedName);

        if (request.EndDate < request.StartDate)
            response.AddResponseMessage(MessageType.ERROR, "EndDate must be on or after StartDate.");

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
        ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response);

        if (request is null)
        {
            response.AddResponseMessage(MessageType.ERROR, "An update event request is required.");
            return response;
        }

        ServiceValidationHelper.TryNormalizeString(request.Name, "Event name", response, out string normalizedName);

        if (request.EndDate < request.StartDate)
            response.AddResponseMessage(MessageType.ERROR, "EndDate must be on or after StartDate.");

        if (ServiceValidationHelper.HasErrors(response))
            return response;

        var @event = await _dbContext.Events
            .Where(e => e.TenantId == tenantId && e.Id == eventId)
            .SingleOrDefaultAsync(cancellationToken);

        if (@event is null)
        {
            response.AddResponseMessage(MessageType.ERROR, "Event not found.");
            return response;
        }

        var oldStart = @event.StartDate;
        var oldEnd = @event.EndDate;

        @event.Name = normalizedName;
        @event.StartDate = request.StartDate;
        @event.EndDate = request.EndDate;

        if (request.StartDate != oldStart || request.EndDate != oldEnd)
            await SyncPlansInternalAsync(tenantId, @event, oldStart, oldEnd, cancellationToken);

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
        ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response);

        if (ServiceValidationHelper.HasErrors(response))
            return response;

        var @event = await _dbContext.Events
            .Where(e => e.TenantId == tenantId && e.Id == eventId)
            .SingleOrDefaultAsync(cancellationToken);

        if (@event is null)
        {
            response.AddResponseMessage(MessageType.ERROR, "Event not found.");
            return response;
        }

        if (@event.IsDeleted)
        {
            response.AddResponseMessage(MessageType.WARNING, "Event already deleted.");
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
        ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response);

        if (ServiceValidationHelper.HasErrors(response))
            return response;

        var @event = await _dbContext.Events
            .Where(e => e.TenantId == tenantId && e.Id == eventId)
            .SingleOrDefaultAsync(cancellationToken);

        if (@event is null)
        {
            response.AddResponseMessage(MessageType.ERROR, "Event not found.");
            return response;
        }

        await SyncPlansInternalAsync(tenantId, @event, @event.StartDate, @event.EndDate, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        response.SetSuccess(MapToDto(@event));
        return response;
    }

    private async Task SyncPlansInternalAsync(
        Guid tenantId,
        Event @event,
        DateOnly oldStart,
        DateOnly oldEnd,
        CancellationToken cancellationToken)
    {
        var newStart = @event.StartDate;
        var newEnd = @event.EndDate;

        var choreTemplates = await _dbContext.ChoreTemplates
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.EventId == @event.Id)
            .ToListAsync(cancellationToken);

        foreach (var template in choreTemplates)
            await ApplyChorePlanDiffAsync(tenantId, template, newStart, newEnd, cancellationToken);

        var mealTemplates = await _dbContext.MealTemplates
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.EventId == @event.Id)
            .ToListAsync(cancellationToken);

        foreach (var template in mealTemplates)
            await ApplyMealPlanDiffAsync(tenantId, template, newStart, newEnd, cancellationToken);
    }

    private async Task ApplyChorePlanDiffAsync(
        Guid tenantId,
        ChoreTemplate template,
        DateOnly start,
        DateOnly end,
        CancellationToken cancellationToken)
    {
        // Load all plans including soft-deleted so PlanGenerator can detect suppression markers.
        // IgnoreQueryFilters() removes the global soft-delete filter; tenant isolation is
        // re-enforced explicitly below since this is an internal operation.
        var existing = await _dbContext.ChorePlans
            .IgnoreQueryFilters()
            .Include(p => p.Intents)
            .Where(p => p.TenantId == tenantId && p.TemplateId == template.Id)
            .ToListAsync(cancellationToken);

        var diff = PlanGenerator.DiffChorePlans(template.TimeSlots, start, end, existing);

        foreach (var (day, slot) in diff.ToAdd)
        {
            _dbContext.ChorePlans.Add(new ChorePlan
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                TemplateId = template.Id,
                Day = day,
                TimeSlot = slot,
                Completed = false,
            });
        }

        foreach (var plan in diff.ToRestore)
            plan.IsDeleted = false;

        foreach (var plan in diff.ToPrune)
            plan.IsDeleted = true;
    }

    private async Task ApplyMealPlanDiffAsync(
        Guid tenantId,
        MealTemplate template,
        DateOnly start,
        DateOnly end,
        CancellationToken cancellationToken)
    {
        var existing = await _dbContext.MealPlans
            .IgnoreQueryFilters()
            .Include(p => p.Intents)
            .Where(p => p.TenantId == tenantId && p.MealTemplateId == template.Id)
            .ToListAsync(cancellationToken);

        var diff = PlanGenerator.DiffMealPlans(template.MealTypes, start, end, existing);

        foreach (var (day, mealType) in diff.ToAdd)
        {
            _dbContext.MealPlans.Add(new MealPlan
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                MealTemplateId = template.Id,
                Day = day,
                MealType = mealType,
            });
        }

        foreach (var plan in diff.ToRestore)
            plan.IsDeleted = false;

        foreach (var plan in diff.ToPrune)
            plan.IsDeleted = true;
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
