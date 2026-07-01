using Gatherstead.Api.Contracts.TaskPlans;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.Validation;
using Gatherstead.Data;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Api.Services.TaskPlans;

public class TaskPlanService : ITaskPlanService
{
    private const string EntityDisplayName = "Task plan";

    private readonly GathersteadDbContext _dbContext;
    private readonly ICurrentTenantContext _currentTenantContext;
    private readonly IMemberAuthorizationService _memberAuthorizationService;
    private readonly IAuditVisibilityContext _auditVisibility;

    public TaskPlanService(
        GathersteadDbContext dbContext,
        ICurrentTenantContext currentTenantContext,
        IMemberAuthorizationService memberAuthorizationService,
        IAuditVisibilityContext auditVisibility)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentTenantContext = currentTenantContext ?? throw new ArgumentNullException(nameof(currentTenantContext));
        _memberAuthorizationService = memberAuthorizationService ?? throw new ArgumentNullException(nameof(memberAuthorizationService));
        _auditVisibility = auditVisibility ?? throw new ArgumentNullException(nameof(auditVisibility));
    }

    public async Task<BaseEntityResponse<IReadOnlyCollection<TaskPlanDto>>> ListAsync(
        Guid tenantId,
        Guid eventId,
        Guid templateId,
        IEnumerable<Guid>? ids = null,
        CancellationToken cancellationToken = default)
    {
        var response = new BaseEntityResponse<IReadOnlyCollection<TaskPlanDto>>();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        var query = _dbContext.TaskPlans
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.TemplateId == templateId);

        if (ids is not null)
        {
            var idList = ids.ToList();
            if (idList.Count > 0)
                query = query.Where(p => idList.Contains(p.Id));
        }

        var entities = await query.ToListAsync(cancellationToken);
        var plans = entities.Select(MapToDto).ToList();

        return BaseEntityResponse<IReadOnlyCollection<TaskPlanDto>>.SuccessfulResponse(plans);
    }

    public async Task<TaskPlanResponse> GetAsync(
        Guid tenantId,
        Guid templateId,
        Guid planId,
        CancellationToken cancellationToken = default)
    {
        var response = new TaskPlanResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        var plan = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.TaskPlans.AsNoTracking()
                .Where(p => p.TenantId == tenantId && p.TemplateId == templateId && p.Id == planId),
            EntityDisplayName,
            cancellationToken);

        if (plan is null) return response;

        response.SetSuccess(MapToDto(plan));
        return response;
    }

    public async Task<TaskPlanResponse> UpdateAsync(
        Guid tenantId,
        Guid templateId,
        Guid planId,
        UpdateTaskPlanRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new TaskPlanResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!ServiceGuards.RequireRequest(request, "update task plan", response))
            return response;
        if (!await ServiceGuards.AuthorizeEventManageAsync(response, _memberAuthorizationService, tenantId, cancellationToken))
            return response;

        var plan = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.TaskPlans.Where(p => p.TenantId == tenantId && p.TemplateId == templateId && p.Id == planId),
            EntityDisplayName,
            cancellationToken);

        if (plan is null) return response;

        plan.Completed = request.Completed;
        plan.Notes = request.Notes?.Trim();
        plan.IsException = request.IsException;
        plan.ExceptionReason = request.ExceptionReason?.Trim();

        await _dbContext.SaveChangesAsync(cancellationToken);

        response.SetSuccess(MapToDto(plan));
        return response;
    }

    public async Task<TaskPlanResponse> DeleteAsync(
        Guid tenantId,
        Guid templateId,
        Guid planId,
        CancellationToken cancellationToken = default)
    {
        var response = new TaskPlanResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!await ServiceGuards.AuthorizeEventManageAsync(response, _memberAuthorizationService, tenantId, cancellationToken))
            return response;

        var plan = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.TaskPlans.Where(p => p.TenantId == tenantId && p.TemplateId == templateId && p.Id == planId),
            EntityDisplayName,
            cancellationToken);

        if (plan is null) return response;

        // Suppression marker: IsDeleted + IsException together tell PlanGenerator to keep this
        // plan removed instead of regenerating it on the next sync (see PlanGenerator.DiffTaskPlans).
        plan.IsDeleted = true;
        plan.IsException = true;

        await _dbContext.SaveChangesAsync(cancellationToken);

        response.SetSuccess(MapToDto(plan));
        return response;
    }

    private TaskPlanDto MapToDto(Data.Entities.TaskPlan p) => new(
        p.Id, p.TenantId, p.TemplateId, p.Day, p.TimeSlot, p.Completed, p.Notes,
        p.IsException, p.ExceptionReason,
        p.ToAuditInfo(_auditVisibility.IncludeAudit));
}
