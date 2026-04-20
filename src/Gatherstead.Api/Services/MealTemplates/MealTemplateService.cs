using Gatherstead.Api.Contracts.MealTemplates;
using Gatherstead.Api.Contracts.Responses;
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

    public MealTemplateService(
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

    public async Task<BaseEntityResponse<IReadOnlyCollection<MealTemplateDto>>> ListAsync(
        Guid tenantId,
        Guid eventId,
        IEnumerable<Guid>? ids = null,
        CancellationToken cancellationToken = default)
    {
        var response = new BaseEntityResponse<IReadOnlyCollection<MealTemplateDto>>();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        var query = _dbContext.MealTemplates
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.EventId == eventId);

        if (ids is not null)
        {
            var idList = ids.ToList();
            if (idList.Count > 0)
                query = query.Where(t => idList.Contains(t.Id));
        }

        var templates = await query.Select(t => MapToDto(t)).ToListAsync(cancellationToken);

        return BaseEntityResponse<IReadOnlyCollection<MealTemplateDto>>.SuccessfulResponse(templates);
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
                .Where(t => t.TenantId == tenantId && t.EventId == eventId && t.Id == templateId),
            EntityDisplayName,
            cancellationToken);

        if (template is null) return response;

        response.SetSuccess(MapToDto(template));
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
        if (!await ServiceGuards.AuthorizeTenantManageAsync(response, _memberAuthorizationService, tenantId, cancellationToken))
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

        var template = new MealTemplate
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EventId = eventId,
            Name = normalizedName,
            MealTypes = request.MealTypes,
            Notes = request.Notes?.Trim(),
        };

        _dbContext.MealTemplates.Add(template);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _planSyncService.SyncMealPlanAsync(tenantId, template, @event.StartDate, @event.EndDate, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        response.SetSuccess(MapToDto(template));
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
        if (!await ServiceGuards.AuthorizeTenantManageAsync(response, _memberAuthorizationService, tenantId, cancellationToken))
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

        template.Name = normalizedName;
        template.MealTypes = request.MealTypes;
        template.Notes = request.Notes?.Trim();

        if (mealTypesChanged)
        {
            var @event = await _dbContext.Events
                .AsNoTracking()
                .Where(e => e.TenantId == tenantId && e.Id == eventId)
                .SingleOrDefaultAsync(cancellationToken);

            if (@event is not null)
                await _planSyncService.SyncMealPlanAsync(tenantId, template, @event.StartDate, @event.EndDate, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        response.SetSuccess(MapToDto(template));
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
        if (!await ServiceGuards.AuthorizeTenantManageAsync(response, _memberAuthorizationService, tenantId, cancellationToken))
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
        await _dbContext.SaveChangesAsync(cancellationToken);

        response.SetSuccess(MapToDto(template));
        return response;
    }

    private static MealTemplateDto MapToDto(MealTemplate t) => new(
        t.Id, t.TenantId, t.EventId, t.Name, t.MealTypes, t.Notes,
        t.CreatedAt, t.UpdatedAt, t.IsDeleted, t.DeletedAt, t.DeletedByUserId);
}
