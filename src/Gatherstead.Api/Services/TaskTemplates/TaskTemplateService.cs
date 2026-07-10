using Gatherstead.Api.Contracts.Attributes;
using Gatherstead.Api.Contracts.TaskTemplates;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Services.Attributes;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.Planning;
using Gatherstead.Api.Services.Validation;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Api.Services.TaskTemplates;

public class TaskTemplateService : ITaskTemplateService
{
    private const string EntityDisplayName = "Task template";

    private readonly GathersteadDbContext _dbContext;
    private readonly ICurrentTenantContext _currentTenantContext;
    private readonly IMemberAuthorizationService _memberAuthorizationService;
    private readonly PlanSyncService _planSyncService;
    private readonly IAuditVisibilityContext _auditVisibility;

    public TaskTemplateService(
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

    public async Task<BaseEntityResponse<IReadOnlyCollection<TaskTemplateDto>>> ListAsync(
        Guid tenantId,
        Guid eventId,
        IEnumerable<Guid>? ids = null,
        CancellationToken cancellationToken = default)
    {
        var response = new BaseEntityResponse<IReadOnlyCollection<TaskTemplateDto>>();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        // Attributes ride along on lists (mirrors GetAsync) so list-sourced edits don't wipe them.
        // A caller with no tenant role sees none, so skip loading them entirely.
        var callerRole = await _memberAuthorizationService.GetCallerTenantRoleAsync(tenantId, cancellationToken);

        var query = _dbContext.TaskTemplates
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

        return BaseEntityResponse<IReadOnlyCollection<TaskTemplateDto>>.SuccessfulResponse(
            templates.Select(t => MapToDto(t, AttributeVisibilityHelper.Visible(t.Attributes, callerRole))).ToList());
    }

    public async Task<TaskTemplateResponse> GetAsync(
        Guid tenantId,
        Guid eventId,
        Guid templateId,
        CancellationToken cancellationToken = default)
    {
        var response = new TaskTemplateResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        var template = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.TaskTemplates.AsNoTracking()
                .Include(t => t.Attributes)
                .Where(t => t.TenantId == tenantId && t.EventId == eventId && t.Id == templateId),
            EntityDisplayName,
            cancellationToken);

        if (template is null) return response;

        var callerRole = await _memberAuthorizationService.GetCallerTenantRoleAsync(tenantId, cancellationToken);
        response.SetSuccess(MapToDto(template, AttributeVisibilityHelper.Visible(template.Attributes, callerRole)));
        return response;
    }

    public async Task<TaskTemplateResponse> CreateAsync(
        Guid tenantId,
        Guid eventId,
        CreateTaskTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new TaskTemplateResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!ServiceGuards.RequireRequest(request, "create task template", response))
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

        var duplicateExists = await _dbContext.TaskTemplates
            .AsNoTracking()
            .AnyAsync(t => t.TenantId == tenantId && t.EventId == eventId && t.Name == normalizedName, cancellationToken);

        if (duplicateExists)
        {
            response.AddResponseMessage(MessageType.ERROR, $"A task template named '{normalizedName}' already exists for this event.");
            return response;
        }

        if (!ValidateDateRange(request.StartDate, request.EndDate, @event.StartDate, @event.EndDate, response))
            return response;

        var template = new TaskTemplate
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EventId = eventId,
            Name = normalizedName,
            TimeSlots = request.TimeSlots,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            MinimumAssignees = request.MinimumAssignees,
            Notes = request.Notes?.Trim(),
        };

        _dbContext.TaskTemplates.Add(template);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _planSyncService.SyncTaskPlanAsync(tenantId, template, @event.StartDate, @event.EndDate, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var callerRole = await _memberAuthorizationService.GetCallerTenantRoleAsync(tenantId, cancellationToken);
        List<AttributeDto> attrs = [];

        if (request.Attributes is { Count: > 0 })
        {
            await AttributeSyncHelper.SyncAsync(
                _dbContext.TaskTemplateAttributes.Where(a => a.TaskTemplateId == template.Id),
                _dbContext.TaskTemplateAttributes,
                request.Attributes,
                a => AttributeVisibilityHelper.IsVisible(a, callerRole),
                tenantId,
                () => new TaskTemplateAttribute { TenantId = tenantId, TaskTemplateId = template.Id },
                applyExtra: null,
                cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var savedAttrs = await _dbContext.TaskTemplateAttributes.AsNoTracking()
                .Where(a => a.TaskTemplateId == template.Id).ToListAsync(cancellationToken);
            attrs = AttributeVisibilityHelper.Visible(savedAttrs, callerRole);
        }

        response.SetSuccess(MapToDto(template, attrs));
        return response;
    }

    public async Task<TaskTemplateResponse> UpdateAsync(
        Guid tenantId,
        Guid eventId,
        Guid templateId,
        UpdateTaskTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new TaskTemplateResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!ServiceGuards.RequireRequest(request, "update task template", response))
            return response;
        if (!await ServiceGuards.AuthorizeEventManageAsync(response, _memberAuthorizationService, tenantId, cancellationToken))
            return response;

        ServiceValidationHelper.TryNormalizeString(request.Name, "Template name", response, out string normalizedName);
        if (ServiceValidationHelper.HasErrors(response))
            return response;

        var template = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.TaskTemplates.Where(t => t.TenantId == tenantId && t.EventId == eventId && t.Id == templateId),
            EntityDisplayName,
            cancellationToken);

        if (template is null) return response;

        if (!string.Equals(template.Name, normalizedName, StringComparison.Ordinal))
        {
            var duplicateExists = await _dbContext.TaskTemplates
                .AsNoTracking()
                .AnyAsync(t => t.TenantId == tenantId && t.EventId == eventId && t.Name == normalizedName && t.Id != templateId, cancellationToken);

            if (duplicateExists)
            {
                response.AddResponseMessage(MessageType.ERROR, $"A task template named '{normalizedName}' already exists for this event.");
                return response;
            }
        }

        var timeSlotsChanged = template.TimeSlots != request.TimeSlots;
        var dateRangeChanged = template.StartDate != request.StartDate || template.EndDate != request.EndDate;

        template.Name = normalizedName;
        template.TimeSlots = request.TimeSlots;
        template.StartDate = request.StartDate;
        template.EndDate = request.EndDate;
        template.MinimumAssignees = request.MinimumAssignees;
        template.Notes = request.Notes?.Trim();

        if (timeSlotsChanged || dateRangeChanged)
        {
            var @event = await _dbContext.Events
                .AsNoTracking()
                .Where(e => e.TenantId == tenantId && e.Id == eventId)
                .SingleOrDefaultAsync(cancellationToken);

            if (@event is not null)
            {
                if (!ValidateDateRange(request.StartDate, request.EndDate, @event.StartDate, @event.EndDate, response))
                    return response;

                await _planSyncService.SyncTaskPlanAsync(tenantId, template, @event.StartDate, @event.EndDate, cancellationToken);
            }
        }

        var callerRole = await _memberAuthorizationService.GetCallerTenantRoleAsync(tenantId, cancellationToken);

        if (request.Attributes is not null)
        {
            await AttributeSyncHelper.SyncAsync(
                _dbContext.TaskTemplateAttributes.Where(a => a.TaskTemplateId == templateId),
                _dbContext.TaskTemplateAttributes,
                request.Attributes,
                a => AttributeVisibilityHelper.IsVisible(a, callerRole),
                tenantId,
                () => new TaskTemplateAttribute { TenantId = tenantId, TaskTemplateId = templateId },
                applyExtra: null,
                cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var savedAttrs = await _dbContext.TaskTemplateAttributes.AsNoTracking()
            .Where(a => a.TaskTemplateId == templateId).ToListAsync(cancellationToken);
        response.SetSuccess(MapToDto(template, AttributeVisibilityHelper.Visible(savedAttrs, callerRole)));
        return response;
    }

    public async Task<TaskTemplateResponse> DeleteAsync(
        Guid tenantId,
        Guid eventId,
        Guid templateId,
        CancellationToken cancellationToken = default)
    {
        var response = new TaskTemplateResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!await ServiceGuards.AuthorizeEventManageAsync(response, _memberAuthorizationService, tenantId, cancellationToken))
            return response;

        var template = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.TaskTemplates.Where(t => t.TenantId == tenantId && t.EventId == eventId && t.Id == templateId),
            EntityDisplayName,
            cancellationToken);

        if (template is null) return response;

        if (template.IsDeleted)
        {
            response.AddResponseMessage(MessageType.WARNING, $"{EntityDisplayName} already deleted.");
            return response;
        }

        template.IsDeleted = true;

        var childAttrs = await _dbContext.TaskTemplateAttributes
            .Where(a => a.TaskTemplateId == templateId).ToListAsync(cancellationToken);
        foreach (var attr in childAttrs)
            attr.IsDeleted = true;

        await _dbContext.SaveChangesAsync(cancellationToken);

        response.SetSuccess(MapToDto(template, []));
        return response;
    }

    private TaskTemplateDto MapToDto(TaskTemplate t, IReadOnlyList<AttributeDto> attributes) => new(
        t.Id, t.TenantId, t.EventId, t.Name, (int)t.TimeSlots,
        t.StartDate, t.EndDate,
        t.MinimumAssignees, t.Notes,
        attributes,
        t.ToAuditInfo(_auditVisibility.IncludeAudit));

    private static bool ValidateDateRange(
        DateOnly? startDate, DateOnly? endDate,
        DateOnly eventStart, DateOnly eventEnd,
        BaseEntityResponse<TaskTemplateDto> response)
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
}
