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

    public MealIntentService(
        GathersteadDbContext dbContext,
        ICurrentTenantContext currentTenantContext,
        IMemberAuthorizationService memberAuthorizationService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentTenantContext = currentTenantContext ?? throw new ArgumentNullException(nameof(currentTenantContext));
        _memberAuthorizationService = memberAuthorizationService ?? throw new ArgumentNullException(nameof(memberAuthorizationService));
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

        var intents = await query.Select(i => MapToDto(i)).ToListAsync(cancellationToken);

        return BaseEntityResponse<IReadOnlyCollection<MealIntentDto>>.SuccessfulResponse(intents);
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
        if (!await ServiceGuards.AuthorizeMemberEditAsync(response, _memberAuthorizationService, tenantId, householdId, request.HouseholdMemberId, cancellationToken))
            return response;

        var planExists = await _dbContext.MealPlans
            .AsNoTracking()
            .AnyAsync(p => p.TenantId == tenantId && p.Id == planId, cancellationToken);

        if (!planExists)
        {
            response.AddResponseMessage(MessageType.ERROR, "Meal plan not found.");
            return response;
        }

        var existing = await _dbContext.MealIntents
            .Where(i => i.TenantId == tenantId && i.MealPlanId == planId && i.HouseholdMemberId == request.HouseholdMemberId)
            .SingleOrDefaultAsync(cancellationToken);

        if (existing is null)
        {
            existing = new MealIntent
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                MealPlanId = planId,
                HouseholdMemberId = request.HouseholdMemberId,
            };
            _dbContext.MealIntents.Add(existing);
        }
        else if (existing.IsDeleted)
        {
            existing.IsDeleted = false;
        }

        existing.Status = request.Status;
        existing.BringOwnFood = request.BringOwnFood;
        existing.Notes = request.Notes?.Trim();

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

        if (!await ServiceGuards.AuthorizeMemberEditAsync(response, _memberAuthorizationService, tenantId, Guid.Empty, intent.HouseholdMemberId, cancellationToken))
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

    private static MealIntentDto MapToDto(MealIntent i) => new(
        i.Id, i.TenantId, i.MealPlanId, i.HouseholdMemberId, i.Status, i.BringOwnFood, i.Notes,
        i.CreatedAt, i.UpdatedAt, i.IsDeleted, i.DeletedAt, i.DeletedByUserId);
}
