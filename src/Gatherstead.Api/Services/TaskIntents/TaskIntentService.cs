using Gatherstead.Api.Contracts.TaskIntents;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.Validation;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Api.Services.TaskIntents;

public class TaskIntentService : ITaskIntentService
{
    private const string EntityDisplayName = "Task intent";

    private readonly GathersteadDbContext _dbContext;
    private readonly ICurrentTenantContext _currentTenantContext;
    private readonly IMemberAuthorizationService _memberAuthorizationService;
    private readonly IAuditVisibilityContext _auditVisibility;

    public TaskIntentService(
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

    public async Task<BaseEntityResponse<IReadOnlyCollection<TaskIntentDto>>> ListAsync(
        Guid tenantId,
        Guid planId,
        IEnumerable<Guid>? memberIds = null,
        CancellationToken cancellationToken = default)
    {
        var response = new BaseEntityResponse<IReadOnlyCollection<TaskIntentDto>>();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        var query = _dbContext.TaskIntents
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId && i.TaskPlanId == planId);

        if (memberIds is not null)
        {
            var memberIdList = memberIds.ToList();
            if (memberIdList.Count > 0)
                query = query.Where(i => memberIdList.Contains(i.HouseholdMemberId));
        }

        var intents = await query.Select(i => MapToDto(i)).ToListAsync(cancellationToken);

        return BaseEntityResponse<IReadOnlyCollection<TaskIntentDto>>.SuccessfulResponse(intents);
    }

    public async Task<TaskIntentResponse> GetAsync(
        Guid tenantId,
        Guid planId,
        Guid intentId,
        CancellationToken cancellationToken = default)
    {
        var response = new TaskIntentResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        var intent = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.TaskIntents.AsNoTracking()
                .Where(i => i.TenantId == tenantId && i.TaskPlanId == planId && i.Id == intentId),
            EntityDisplayName,
            cancellationToken);

        if (intent is null) return response;

        response.SetSuccess(MapToDto(intent));
        return response;
    }

    public async Task<TaskIntentResponse> UpsertAsync(
        Guid tenantId,
        Guid planId,
        Guid householdId,
        UpsertTaskIntentRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new TaskIntentResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!ServiceGuards.RequireRequest(request, "upsert task intent", response))
            return response;
        if (!await ServiceGuards.AuthorizeIntentAssignAsync(response, _memberAuthorizationService, tenantId, householdId, request.HouseholdMemberId, cancellationToken))
            return response;

        var planExists = await _dbContext.TaskPlans
            .AsNoTracking()
            .AnyAsync(p => p.TenantId == tenantId && p.Id == planId, cancellationToken);

        if (!planExists)
        {
            response.AddResponseMessage(MessageType.ERROR, "Task plan not found.");
            return response;
        }

        var existing = await _dbContext.TaskIntents
            .Where(i => i.TenantId == tenantId && i.TaskPlanId == planId && i.HouseholdMemberId == request.HouseholdMemberId)
            .SingleOrDefaultAsync(cancellationToken);

        if (existing is null)
        {
            existing = new TaskIntent
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                TaskPlanId = planId,
                HouseholdMemberId = request.HouseholdMemberId,
            };
            _dbContext.TaskIntents.Add(existing);
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

    public async Task<TaskIntentResponse> DeleteAsync(
        Guid tenantId,
        Guid planId,
        Guid intentId,
        CancellationToken cancellationToken = default)
    {
        var response = new TaskIntentResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        var intent = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.TaskIntents
                .Where(i => i.TenantId == tenantId && i.TaskPlanId == planId && i.Id == intentId),
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

    private TaskIntentDto MapToDto(TaskIntent i) => new(
        i.Id, i.TenantId, i.TaskPlanId, i.HouseholdMemberId, i.Volunteered,
        i.ToAuditInfo(_auditVisibility.IncludeAudit));
}
