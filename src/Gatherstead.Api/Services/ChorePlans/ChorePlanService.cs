using Gatherstead.Api.Contracts.ChorePlans;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.Validation;
using Gatherstead.Data;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Api.Services.ChorePlans;

public class ChorePlanService : IChorePlanService
{
    private const string EntityDisplayName = "Chore plan";

    private readonly GathersteadDbContext _dbContext;
    private readonly ICurrentTenantContext _currentTenantContext;
    private readonly IMemberAuthorizationService _memberAuthorizationService;

    public ChorePlanService(
        GathersteadDbContext dbContext,
        ICurrentTenantContext currentTenantContext,
        IMemberAuthorizationService memberAuthorizationService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentTenantContext = currentTenantContext ?? throw new ArgumentNullException(nameof(currentTenantContext));
        _memberAuthorizationService = memberAuthorizationService ?? throw new ArgumentNullException(nameof(memberAuthorizationService));
    }

    public async Task<BaseEntityResponse<IReadOnlyCollection<ChorePlanDto>>> ListAsync(
        Guid tenantId,
        Guid eventId,
        Guid templateId,
        IEnumerable<Guid>? ids = null,
        CancellationToken cancellationToken = default)
    {
        var response = new BaseEntityResponse<IReadOnlyCollection<ChorePlanDto>>();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        var query = _dbContext.ChorePlans
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.TemplateId == templateId);

        if (ids is not null)
        {
            var idList = ids.ToList();
            if (idList.Count > 0)
                query = query.Where(p => idList.Contains(p.Id));
        }

        var plans = await query.Select(p => MapToDto(p)).ToListAsync(cancellationToken);

        return BaseEntityResponse<IReadOnlyCollection<ChorePlanDto>>.SuccessfulResponse(plans);
    }

    public async Task<ChorePlanResponse> GetAsync(
        Guid tenantId,
        Guid templateId,
        Guid planId,
        CancellationToken cancellationToken = default)
    {
        var response = new ChorePlanResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        var plan = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.ChorePlans.AsNoTracking()
                .Where(p => p.TenantId == tenantId && p.TemplateId == templateId && p.Id == planId),
            EntityDisplayName,
            cancellationToken);

        if (plan is null) return response;

        response.SetSuccess(MapToDto(plan));
        return response;
    }

    public async Task<ChorePlanResponse> UpdateAsync(
        Guid tenantId,
        Guid templateId,
        Guid planId,
        UpdateChorePlanRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new ChorePlanResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!ServiceGuards.RequireRequest(request, "update chore plan", response))
            return response;
        if (!await ServiceGuards.AuthorizeTenantManageAsync(response, _memberAuthorizationService, tenantId, cancellationToken))
            return response;

        var plan = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.ChorePlans.Where(p => p.TenantId == tenantId && p.TemplateId == templateId && p.Id == planId),
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

    private static ChorePlanDto MapToDto(Data.Entities.ChorePlan p) => new(
        p.Id, p.TenantId, p.TemplateId, p.Day, p.TimeSlot, p.Completed, p.Notes,
        p.IsException, p.ExceptionReason,
        p.CreatedAt, p.UpdatedAt, p.IsDeleted, p.DeletedAt, p.DeletedByUserId);
}
