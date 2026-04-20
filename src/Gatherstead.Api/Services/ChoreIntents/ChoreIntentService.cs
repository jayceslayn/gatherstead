using Gatherstead.Api.Contracts.ChoreIntents;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.Validation;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Api.Services.ChoreIntents;

public class ChoreIntentService : IChoreIntentService
{
    private const string EntityDisplayName = "Chore intent";

    private readonly GathersteadDbContext _dbContext;
    private readonly ICurrentTenantContext _currentTenantContext;
    private readonly IMemberAuthorizationService _memberAuthorizationService;

    public ChoreIntentService(
        GathersteadDbContext dbContext,
        ICurrentTenantContext currentTenantContext,
        IMemberAuthorizationService memberAuthorizationService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentTenantContext = currentTenantContext ?? throw new ArgumentNullException(nameof(currentTenantContext));
        _memberAuthorizationService = memberAuthorizationService ?? throw new ArgumentNullException(nameof(memberAuthorizationService));
    }

    public async Task<BaseEntityResponse<IReadOnlyCollection<ChoreIntentDto>>> ListAsync(
        Guid tenantId,
        Guid planId,
        IEnumerable<Guid>? memberIds = null,
        CancellationToken cancellationToken = default)
    {
        var response = new BaseEntityResponse<IReadOnlyCollection<ChoreIntentDto>>();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        var query = _dbContext.ChoreIntents
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId && i.ChorePlanId == planId);

        if (memberIds is not null)
        {
            var memberIdList = memberIds.ToList();
            if (memberIdList.Count > 0)
                query = query.Where(i => memberIdList.Contains(i.HouseholdMemberId));
        }

        var intents = await query.Select(i => MapToDto(i)).ToListAsync(cancellationToken);

        return BaseEntityResponse<IReadOnlyCollection<ChoreIntentDto>>.SuccessfulResponse(intents);
    }

    public async Task<ChoreIntentResponse> GetAsync(
        Guid tenantId,
        Guid planId,
        Guid intentId,
        CancellationToken cancellationToken = default)
    {
        var response = new ChoreIntentResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        var intent = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.ChoreIntents.AsNoTracking()
                .Where(i => i.TenantId == tenantId && i.ChorePlanId == planId && i.Id == intentId),
            EntityDisplayName,
            cancellationToken);

        if (intent is null) return response;

        response.SetSuccess(MapToDto(intent));
        return response;
    }

    public async Task<ChoreIntentResponse> UpsertAsync(
        Guid tenantId,
        Guid planId,
        Guid householdId,
        UpsertChoreIntentRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new ChoreIntentResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!ServiceGuards.RequireRequest(request, "upsert chore intent", response))
            return response;
        if (!await ServiceGuards.AuthorizeMemberEditAsync(response, _memberAuthorizationService, tenantId, householdId, request.HouseholdMemberId, cancellationToken))
            return response;

        var planExists = await _dbContext.ChorePlans
            .AsNoTracking()
            .AnyAsync(p => p.TenantId == tenantId && p.Id == planId, cancellationToken);

        if (!planExists)
        {
            response.AddResponseMessage(MessageType.ERROR, "Chore plan not found.");
            return response;
        }

        var existing = await _dbContext.ChoreIntents
            .Where(i => i.TenantId == tenantId && i.ChorePlanId == planId && i.HouseholdMemberId == request.HouseholdMemberId)
            .SingleOrDefaultAsync(cancellationToken);

        if (existing is null)
        {
            existing = new ChoreIntent
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ChorePlanId = planId,
                HouseholdMemberId = request.HouseholdMemberId,
            };
            _dbContext.ChoreIntents.Add(existing);
        }
        else if (existing.IsDeleted)
        {
            existing.IsDeleted = false;
        }

        existing.Volunteered = request.Volunteered;

        await _dbContext.SaveChangesAsync(cancellationToken);

        response.SetSuccess(MapToDto(existing));
        return response;
    }

    public async Task<ChoreIntentResponse> DeleteAsync(
        Guid tenantId,
        Guid planId,
        Guid intentId,
        CancellationToken cancellationToken = default)
    {
        var response = new ChoreIntentResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        var intent = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.ChoreIntents
                .Where(i => i.TenantId == tenantId && i.ChorePlanId == planId && i.Id == intentId),
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

    private static ChoreIntentDto MapToDto(ChoreIntent i) => new(
        i.Id, i.TenantId, i.ChorePlanId, i.HouseholdMemberId, i.Volunteered,
        i.CreatedAt, i.UpdatedAt, i.IsDeleted, i.DeletedAt, i.DeletedByUserId);
}
