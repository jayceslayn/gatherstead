using Gatherstead.Api.Contracts.Attributes;
using Gatherstead.Api.Contracts.MealTemplates;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Services.Attributes;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.Planning;
using Gatherstead.Api.Services.Validation;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Api.Services.MealTemplates;

public class MealTemplateService : IMealTemplateService
{
    private const string EntityDisplayName = "Meal template";

    private readonly GathersteadDbContext _dbContext;
    private readonly ICurrentTenantContext _currentTenantContext;
    private readonly IMemberAuthorizationService _memberAuthorizationService;
    private readonly PlanSyncService _planSyncService;
    private readonly IAuditVisibilityContext _auditVisibility;

    public MealTemplateService(
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

    public async Task<BaseEntityResponse<IReadOnlyCollection<MealTemplateDto>>> ListAsync(
        Guid tenantId,
        Guid eventId,
        IEnumerable<Guid>? ids = null,
        CancellationToken cancellationToken = default)
    {
        var response = new BaseEntityResponse<IReadOnlyCollection<MealTemplateDto>>();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        // Attributes ride along on lists (mirrors GetAsync) so list-sourced edits don't wipe them.
        // A caller with no tenant role sees none, so skip loading them entirely.
        var callerRole = await _memberAuthorizationService.GetCallerTenantRoleAsync(tenantId, cancellationToken);

        var query = _dbContext.MealTemplates
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.EventId == eventId);
        if (callerRole is not null)
            query = query.Include(t => t.Attributes);

        if (ids is not null)
        {
            var idList = ids.ToList();
            if (idList.Count > 0)
                query = query.Where(t => idList.Contains(t.Id));
        }

        var templates = await query.ToListAsync(cancellationToken);

        return BaseEntityResponse<IReadOnlyCollection<MealTemplateDto>>.SuccessfulResponse(
            templates.Select(t => MapToDto(t, AttributeVisibilityHelper.Visible(t.Attributes, callerRole))).ToList());
    }

    public async Task<MealTemplateResponse> GetAsync(
        Guid tenantId,
        Guid eventId,
        Guid templateId,
        CancellationToken cancellationToken = default)
    {
        var response = new MealTemplateResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        var template = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.MealTemplates.AsNoTracking()
                .Include(t => t.Attributes)
                .Where(t => t.TenantId == tenantId && t.EventId == eventId && t.Id == templateId),
            EntityDisplayName,
            cancellationToken);

        if (template is null) return response;

        var callerRole = await _memberAuthorizationService.GetCallerTenantRoleAsync(tenantId, cancellationToken);
        response.SetSuccess(MapToDto(template, AttributeVisibilityHelper.Visible(template.Attributes, callerRole)));
        return response;
    }

    public async Task<MealTemplateResponse> CreateAsync(
        Guid tenantId,
        Guid eventId,
        CreateMealTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new MealTemplateResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!ServiceGuards.RequireRequest(request, "create meal template", response))
            return response;
        if (!await ServiceGuards.AuthorizeEventManageAsync(response, _memberAuthorizationService, tenantId, cancellationToken))
            return response;

        ServiceValidationHelper.TryNormalizeString(request.Name, "Template name", response, out string normalizedName);
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

        var duplicateExists = await _dbContext.MealTemplates
            .AsNoTracking()
            .AnyAsync(t => t.TenantId == tenantId && t.EventId == eventId && t.Name == normalizedName, cancellationToken);

        if (duplicateExists)
        {
            response.AddResponseMessage(MessageType.ERROR, $"A meal template named '{normalizedName}' already exists for this event.");
            return response;
        }

        if (!ValidateDateRange(request.StartDate, request.EndDate, @event.StartDate, @event.EndDate, response))
            return response;

        var template = new MealTemplate
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EventId = eventId,
            Name = normalizedName,
            MealTypes = request.MealTypes,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Notes = request.Notes?.Trim(),
        };

        _dbContext.MealTemplates.Add(template);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _planSyncService.SyncMealPlanAsync(tenantId, template, @event.StartDate, @event.EndDate, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (request.CreateMatchingTaskTemplate)
        {
            await CreateMatchingTaskTemplateAsync(tenantId, eventId, template, @event, cancellationToken);
        }

        var callerRole = await _memberAuthorizationService.GetCallerTenantRoleAsync(tenantId, cancellationToken);
        List<AttributeDto> attrs = [];

        if (request.Attributes is { Count: > 0 })
        {
            await AttributeSyncHelper.SyncAsync(
                _dbContext.MealTemplateAttributes.Where(a => a.MealTemplateId == template.Id),
                _dbContext.MealTemplateAttributes,
                request.Attributes,
                a => AttributeVisibilityHelper.IsVisible(a, callerRole),
                tenantId,
                () => new MealTemplateAttribute { TenantId = tenantId, MealTemplateId = template.Id },
                applyExtra: null,
                cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var savedAttrs = await _dbContext.MealTemplateAttributes.AsNoTracking()
                .Where(a => a.MealTemplateId == template.Id).ToListAsync(cancellationToken);
            attrs = AttributeVisibilityHelper.Visible(savedAttrs, callerRole);
        }

        response.SetSuccess(MapToDto(template, attrs));
        return response;
    }

    public async Task<MealTemplateResponse> UpdateAsync(
        Guid tenantId,
        Guid eventId,
        Guid templateId,
        UpdateMealTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new MealTemplateResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!ServiceGuards.RequireRequest(request, "update meal template", response))
            return response;
        if (!await ServiceGuards.AuthorizeEventManageAsync(response, _memberAuthorizationService, tenantId, cancellationToken))
            return response;

        ServiceValidationHelper.TryNormalizeString(request.Name, "Template name", response, out string normalizedName);
        if (ServiceValidationHelper.HasErrors(response))
            return response;

        var template = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.MealTemplates.Where(t => t.TenantId == tenantId && t.EventId == eventId && t.Id == templateId),
            EntityDisplayName,
            cancellationToken);

        if (template is null) return response;

        if (!string.Equals(template.Name, normalizedName, StringComparison.Ordinal))
        {
            var duplicateExists = await _dbContext.MealTemplates
                .AsNoTracking()
                .AnyAsync(t => t.TenantId == tenantId && t.EventId == eventId && t.Name == normalizedName && t.Id != templateId, cancellationToken);

            if (duplicateExists)
            {
                response.AddResponseMessage(MessageType.ERROR, $"A meal template named '{normalizedName}' already exists for this event.");
                return response;
            }
        }

        var mealTypesChanged = template.MealTypes != request.MealTypes;
        var dateRangeChanged = template.StartDate != request.StartDate || template.EndDate != request.EndDate;

        template.Name = normalizedName;
        template.MealTypes = request.MealTypes;
        template.StartDate = request.StartDate;
        template.EndDate = request.EndDate;
        template.Notes = request.Notes?.Trim();

        if (mealTypesChanged || dateRangeChanged)
        {
            var @event = await _dbContext.Events
                .AsNoTracking()
                .Where(e => e.TenantId == tenantId && e.Id == eventId)
                .SingleOrDefaultAsync(cancellationToken);

            if (@event is not null)
            {
                if (!ValidateDateRange(request.StartDate, request.EndDate, @event.StartDate, @event.EndDate, response))
                    return response;

                await _planSyncService.SyncMealPlanAsync(tenantId, template, @event.StartDate, @event.EndDate, cancellationToken);
            }
        }

        var callerRole = await _memberAuthorizationService.GetCallerTenantRoleAsync(tenantId, cancellationToken);

        if (request.Attributes is not null)
        {
            await AttributeSyncHelper.SyncAsync(
                _dbContext.MealTemplateAttributes.Where(a => a.MealTemplateId == templateId),
                _dbContext.MealTemplateAttributes,
                request.Attributes,
                a => AttributeVisibilityHelper.IsVisible(a, callerRole),
                tenantId,
                () => new MealTemplateAttribute { TenantId = tenantId, MealTemplateId = templateId },
                applyExtra: null,
                cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var savedAttrs = await _dbContext.MealTemplateAttributes.AsNoTracking()
            .Where(a => a.MealTemplateId == templateId).ToListAsync(cancellationToken);
        response.SetSuccess(MapToDto(template, AttributeVisibilityHelper.Visible(savedAttrs, callerRole)));
        return response;
    }

    public async Task<MealTemplateResponse> DeleteAsync(
        Guid tenantId,
        Guid eventId,
        Guid templateId,
        CancellationToken cancellationToken = default)
    {
        var response = new MealTemplateResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!await ServiceGuards.AuthorizeEventManageAsync(response, _memberAuthorizationService, tenantId, cancellationToken))
            return response;

        var template = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.MealTemplates.Where(t => t.TenantId == tenantId && t.EventId == eventId && t.Id == templateId),
            EntityDisplayName,
            cancellationToken);

        if (template is null) return response;

        if (template.IsDeleted)
        {
            response.AddResponseMessage(MessageType.WARNING, $"{EntityDisplayName} already deleted.");
            return response;
        }

        template.IsDeleted = true;

        var childAttrs = await _dbContext.MealTemplateAttributes
            .Where(a => a.MealTemplateId == templateId).ToListAsync(cancellationToken);
        foreach (var attr in childAttrs)
            attr.IsDeleted = true;

        await _dbContext.SaveChangesAsync(cancellationToken);

        response.SetSuccess(MapToDto(template, []));
        return response;
    }

    private MealTemplateDto MapToDto(MealTemplate t, IReadOnlyList<AttributeDto> attributes) => new(
        t.Id, t.TenantId, t.EventId, t.Name, (int)t.MealTypes,
        t.StartDate, t.EndDate,
        t.Notes,
        attributes,
        t.ToAuditInfo(_auditVisibility.IncludeAudit));

    private static bool ValidateDateRange(
        DateOnly? startDate, DateOnly? endDate,
        DateOnly eventStart, DateOnly eventEnd,
        BaseEntityResponse<MealTemplateDto> response)
    {
        if (startDate is null && endDate is null)
            return true;

        if (startDate is null || endDate is null)
        {
            response.AddResponseMessage(MessageType.ERROR, "StartDate and EndDate must both be set or both be null.");
            return false;
        }

        if (startDate > endDate)
        {
            response.AddResponseMessage(MessageType.ERROR, "StartDate must not be after EndDate.");
            return false;
        }

        if (startDate < eventStart || endDate > eventEnd)
        {
            response.AddResponseMessage(MessageType.ERROR, "StartDate and EndDate must fall within the event date range.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Maps a meal's <see cref="MealTypeFlags"/> to the equivalent <see cref="TaskTimeSlotFlags"/>:
    /// Breakfast→Morning, Lunch→Midday, Dinner→Evening. Falls back to Anytime when no meal type is set.
    /// </summary>
    private static TaskTimeSlotFlags MapMealTypesToTimeSlots(MealTypeFlags mealTypes)
    {
        TaskTimeSlotFlags slots = 0;
        if (mealTypes.HasFlag(MealTypeFlags.Breakfast)) slots |= TaskTimeSlotFlags.Morning;
        if (mealTypes.HasFlag(MealTypeFlags.Lunch)) slots |= TaskTimeSlotFlags.Midday;
        if (mealTypes.HasFlag(MealTypeFlags.Dinner)) slots |= TaskTimeSlotFlags.Evening;
        return slots == 0 ? TaskTimeSlotFlags.Anytime : slots;
    }

    /// <summary>
    /// Creates a TaskTemplate mirroring a meal template so the meal can be organized/assigned.
    /// Skips silently if a task template name conflict cannot be resolved, keeping meal creation successful.
    /// </summary>
    private async Task CreateMatchingTaskTemplateAsync(
        Guid tenantId,
        Guid eventId,
        MealTemplate mealTemplate,
        Event @event,
        CancellationToken cancellationToken)
    {
        var existingNames = await _dbContext.TaskTemplates
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.EventId == eventId)
            .Select(t => t.Name)
            .ToListAsync(cancellationToken);

        var nameSet = existingNames.ToHashSet(StringComparer.Ordinal);
        var candidate = mealTemplate.Name;
        if (nameSet.Contains(candidate))
            candidate = $"{mealTemplate.Name} (cook)";
        if (nameSet.Contains(candidate))
            return; // Can't find a non-conflicting name; leave the meal as-is.

        var taskTemplate = new TaskTemplate
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EventId = eventId,
            Name = candidate,
            TimeSlots = MapMealTypesToTimeSlots(mealTemplate.MealTypes),
            StartDate = mealTemplate.StartDate,
            EndDate = mealTemplate.EndDate,
            Notes = mealTemplate.Notes,
        };

        _dbContext.TaskTemplates.Add(taskTemplate);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _planSyncService.SyncTaskPlanAsync(tenantId, taskTemplate, @event.StartDate, @event.EndDate, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
