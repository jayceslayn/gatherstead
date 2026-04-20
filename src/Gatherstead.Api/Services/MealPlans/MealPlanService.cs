using Gatherstead.Api.Contracts.MealPlans;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.Validation;
using Gatherstead.Data;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Api.Services.MealPlans;

public class MealPlanService : IMealPlanService
{
    private const string EntityDisplayName = "Meal plan";

    private readonly GathersteadDbContext _dbContext;
    private readonly ICurrentTenantContext _currentTenantContext;
    private readonly IMemberAuthorizationService _memberAuthorizationService;

    public MealPlanService(
        GathersteadDbContext dbContext,
        ICurrentTenantContext currentTenantContext,
        IMemberAuthorizationService memberAuthorizationService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentTenantContext = currentTenantContext ?? throw new ArgumentNullException(nameof(currentTenantContext));
        _memberAuthorizationService = memberAuthorizationService ?? throw new ArgumentNullException(nameof(memberAuthorizationService));
    }

    public async Task<BaseEntityResponse<IReadOnlyCollection<MealPlanDto>>> ListAsync(
        Guid tenantId,
        Guid eventId,
        Guid templateId,
        IEnumerable<Guid>? ids = null,
        CancellationToken cancellationToken = default)
    {
        var response = new BaseEntityResponse<IReadOnlyCollection<MealPlanDto>>();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        var query = _dbContext.MealPlans
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.MealTemplateId == templateId);

        if (ids is not null)
        {
            var idList = ids.ToList();
            if (idList.Count > 0)
                query = query.Where(p => idList.Contains(p.Id));
        }

        var plans = await query.Select(p => MapToDto(p)).ToListAsync(cancellationToken);

        return BaseEntityResponse<IReadOnlyCollection<MealPlanDto>>.SuccessfulResponse(plans);
    }

    public async Task<MealPlanResponse> GetAsync(
        Guid tenantId,
        Guid templateId,
        Guid planId,
        CancellationToken cancellationToken = default)
    {
        var response = new MealPlanResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        var plan = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.MealPlans.AsNoTracking()
                .Where(p => p.TenantId == tenantId && p.MealTemplateId == templateId && p.Id == planId),
            EntityDisplayName,
            cancellationToken);

        if (plan is null) return response;

        response.SetSuccess(MapToDto(plan));
        return response;
    }

    public async Task<MealPlanResponse> UpdateAsync(
        Guid tenantId,
        Guid templateId,
        Guid planId,
        UpdateMealPlanRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new MealPlanResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!ServiceGuards.RequireRequest(request, "update meal plan", response))
            return response;
        if (!await ServiceGuards.AuthorizeTenantManageAsync(response, _memberAuthorizationService, tenantId, cancellationToken))
            return response;

        var plan = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.MealPlans.Where(p => p.TenantId == tenantId && p.MealTemplateId == templateId && p.Id == planId),
            EntityDisplayName,
            cancellationToken);

        if (plan is null) return response;

        plan.Notes = request.Notes?.Trim();
        plan.IsException = request.IsException;
        plan.ExceptionReason = request.ExceptionReason?.Trim();

        await _dbContext.SaveChangesAsync(cancellationToken);

        response.SetSuccess(MapToDto(plan));
        return response;
    }

    private static MealPlanDto MapToDto(Data.Entities.MealPlan p) => new(
        p.Id, p.TenantId, p.MealTemplateId, p.Day, p.MealType, p.Notes,
        p.IsException, p.ExceptionReason,
        p.CreatedAt, p.UpdatedAt, p.IsDeleted, p.DeletedAt, p.DeletedByUserId);
}
