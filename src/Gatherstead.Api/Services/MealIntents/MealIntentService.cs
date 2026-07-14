using Gatherstead.Api.Contracts.MealIntents;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.Validation;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Api.Services.MealIntents;

public class MealIntentService : IMealIntentService
{
    private const string EntityDisplayName = "Meal intent";

    private readonly GathersteadDbContext _dbContext;
    private readonly ICurrentTenantContext _currentTenantContext;
    private readonly IMemberAuthorizationService _memberAuthorizationService;
    private readonly IAuditVisibilityContext _auditVisibility;

    public MealIntentService(
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

    public async Task<BaseEntityResponse<IReadOnlyCollection<MealIntentDto>>> ListAsync(
        Guid tenantId,
        Guid planId,
        IEnumerable<Guid>? memberIds = null,
        CancellationToken cancellationToken = default)
    {
        var response = new BaseEntityResponse<IReadOnlyCollection<MealIntentDto>>();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        var query = _dbContext.MealIntents
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId && i.MealPlanId == planId);

        if (memberIds is not null)
        {
            var memberIdList = memberIds.ToList();
            if (memberIdList.Count > 0)
                query = query.Where(i => memberIdList.Contains(i.HouseholdMemberId));
        }

        var entities = await query.ToListAsync(cancellationToken);
        var intents = entities.Select(MapToDto).ToList();

        return BaseEntityResponse<IReadOnlyCollection<MealIntentDto>>.SuccessfulResponse(intents);
    }

    public async Task<BaseEntityResponse<IReadOnlyCollection<MyMealDto>>> ListForMemberAsync(
        Guid tenantId,
        IEnumerable<Guid>? memberIds = null,
        DateOnly? fromDay = null,
        CancellationToken cancellationToken = default)
    {
        var response = new BaseEntityResponse<IReadOnlyCollection<MyMealDto>>();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        // Every intent row is a cook sign-up (withdrawal deletes the row), so no Source filter is applied.
        var query = _dbContext.MealIntents
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId);

        if (memberIds is not null)
        {
            var memberIdList = memberIds.ToList();
            if (memberIdList.Count > 0)
                query = query.Where(i => memberIdList.Contains(i.HouseholdMemberId));
        }

        if (fromDay is DateOnly from)
            query = query.Where(i => i.MealPlan!.Day >= from);

        var meals = await query
            .OrderBy(i => i.MealPlan!.Day)
            .ThenBy(i => i.MealPlan!.MealType)
            .Select(i => new MyMealDto(
                i.Id,
                i.MealPlanId,
                i.HouseholdMemberId,
                i.MealPlan!.MealTemplateId,
                i.MealPlan.MealTemplate!.Name,
                i.MealPlan.MealTemplate.EventId,
                i.MealPlan.MealTemplate.Event!.Name,
                i.MealPlan.Day,
                i.MealPlan.MealType,
                i.MealPlan.Notes,
                i.Source))
            .ToListAsync(cancellationToken);

        return BaseEntityResponse<IReadOnlyCollection<MyMealDto>>.SuccessfulResponse(meals);
    }

    public async Task<MealIntentResponse> GetAsync(
        Guid tenantId,
        Guid planId,
        Guid intentId,
        CancellationToken cancellationToken = default)
    {
        var response = new MealIntentResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        var intent = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.MealIntents.AsNoTracking()
                .Where(i => i.TenantId == tenantId && i.MealPlanId == planId && i.Id == intentId),
            EntityDisplayName,
            cancellationToken);

        if (intent is null) return response;

        response.SetSuccess(MapToDto(intent));
        return response;
    }

    public async Task<MealIntentResponse> UpsertAsync(
        Guid tenantId,
        Guid planId,
        Guid householdId,
        UpsertMealIntentRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new MealIntentResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!ServiceGuards.RequireRequest(request, "upsert meal intent", response))
            return response;
        // Resolve + authorize the member by its own id; the client-supplied householdId is
        // advisory only (a member has exactly one household), so a stale/mismatched value no
        // longer produces a spurious "Household member not found."
        var source = await ServiceGuards.ResolveMemberForIntentAsync(response, _memberAuthorizationService, _dbContext, tenantId, request.HouseholdMemberId, cancellationToken);
        if (source is null)
            return response;

        var planExists = await _dbContext.MealPlans
            .AsNoTracking()
            .AnyAsync(p => p.TenantId == tenantId && p.Id == planId, cancellationToken);

        if (!planExists)
        {
            response.AddResponseMessage(MessageType.ERROR, "Meal plan not found.");
            return response;
        }

        // Include soft-deleted rows so a re-sign-up after a withdrawal revives the existing row rather
        // than colliding with it on the unique (TenantId, MealPlanId, HouseholdMemberId) index.
        var existing = await _dbContext.MealIntents
            .IgnoreQueryFilters([GathersteadDbContext.SoftDeleteFilter])
            .Where(i => i.TenantId == tenantId && i.MealPlanId == planId && i.HouseholdMemberId == request.HouseholdMemberId)
            .SingleOrDefaultAsync(cancellationToken);

        // Source records who initiated the sign-up; set on create or revive, preserved on a re-upsert
        // of an already-live row.
        if (existing is null)
        {
            existing = new MealIntent
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                MealPlanId = planId,
                HouseholdMemberId = request.HouseholdMemberId,
                Source = source.Value,
            };
            _dbContext.MealIntents.Add(existing);
        }
        else if (existing.IsDeleted)
        {
            existing.IsDeleted = false;
            existing.Source = source.Value;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        response.SetSuccess(MapToDto(existing));
        return response;
    }

    public async Task<MealIntentResponse> DeleteAsync(
        Guid tenantId,
        Guid planId,
        Guid intentId,
        CancellationToken cancellationToken = default)
    {
        var response = new MealIntentResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        var intent = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.MealIntents
                .Where(i => i.TenantId == tenantId && i.MealPlanId == planId && i.Id == intentId),
            EntityDisplayName,
            cancellationToken);

        if (intent is null) return response;

        if (!await ServiceGuards.AuthorizeIntentAssignAsync(response, _memberAuthorizationService, tenantId, Guid.Empty, intent.HouseholdMemberId, cancellationToken))
            return response;

        if (intent.IsDeleted)
        {
            response.AddResponseMessage(MessageType.WARNING, $"{EntityDisplayName} already deleted.");
            return response;
        }

        intent.IsDeleted = true;
        await _dbContext.SaveChangesAsync(cancellationToken);

        response.SetSuccess(MapToDto(intent));
        return response;
    }

    private MealIntentDto MapToDto(MealIntent i) => new(
        i.Id, i.TenantId, i.MealPlanId, i.HouseholdMemberId, i.Source,
        i.ToAuditInfo(_auditVisibility.IncludeAudit));
}
