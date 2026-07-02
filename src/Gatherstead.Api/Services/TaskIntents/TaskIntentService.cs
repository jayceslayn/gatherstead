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

        var entities = await query.ToListAsync(cancellationToken);
        var intents = entities.Select(MapToDto).ToList();

        return BaseEntityResponse<IReadOnlyCollection<TaskIntentDto>>.SuccessfulResponse(intents);
    }

    public async Task<BaseEntityResponse<IReadOnlyCollection<MyTaskDto>>> ListForMemberAsync(
        Guid tenantId,
        IEnumerable<Guid>? memberIds = null,
        DateOnly? fromDay = null,
        CancellationToken cancellationToken = default)
    {
        var response = new BaseEntityResponse<IReadOnlyCollection<MyTaskDto>>();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        var query = _dbContext.TaskIntents
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId && i.Volunteered);

        if (memberIds is not null)
        {
            var memberIdList = memberIds.ToList();
            if (memberIdList.Count > 0)
                query = query.Where(i => memberIdList.Contains(i.HouseholdMemberId));
        }

        if (fromDay is DateOnly from)
            query = query.Where(i => i.TaskPlan!.Day >= from);

        var tasks = await query
            .OrderBy(i => i.TaskPlan!.Day)
            .Select(i => new MyTaskDto(
                i.Id,
                i.TaskPlanId,
                i.HouseholdMemberId,
                i.TaskPlan!.Template!.Name,
                i.TaskPlan.Template.EventId,
                i.TaskPlan.Template.Event!.Name,
                i.TaskPlan.Day,
                i.TaskPlan.TimeSlot,
                i.TaskPlan.Completed,
                i.Volunteered))
            .ToListAsync(cancellationToken);

        return BaseEntityResponse<IReadOnlyCollection<MyTaskDto>>.SuccessfulResponse(tasks);
    }

    public async Task<BaseEntityResponse<IReadOnlyCollection<TaskIntentDto>>> ListForEventAsync(
        Guid tenantId,
        Guid eventId,
        IEnumerable<Guid>? memberIds = null,
        CancellationToken cancellationToken = default)
    {
        var response = new BaseEntityResponse<IReadOnlyCollection<TaskIntentDto>>();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        var query = _dbContext.TaskIntents
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId && i.TaskPlan!.Template!.EventId == eventId);

        if (memberIds is not null)
        {
            var memberIdList = memberIds.ToList();
            if (memberIdList.Count > 0)
                query = query.Where(i => memberIdList.Contains(i.HouseholdMemberId));
        }

        var entities = await query.ToListAsync(cancellationToken);
        var intents = entities.Select(MapToDto).ToList();

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
        // Resolve + authorize the member by its own id; the client-supplied householdId is
        // advisory only (a member has exactly one household), so a stale/mismatched value no
        // longer produces a spurious "Household member not found."
        if (await ServiceGuards.ResolveMemberForIntentAsync(response, _memberAuthorizationService, _dbContext, tenantId, request.HouseholdMemberId, cancellationToken) is null)
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

    public async Task<BulkUpsertResponse<TaskIntentDto>> BulkUpsertAsync(
        Guid tenantId,
        Guid eventId,
        BulkUpsertTaskIntentRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new BulkUpsertResponse<TaskIntentDto>();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!ServiceGuards.RequireRequest(request, "bulk upsert task intent", response))
            return response;

        var items = request.Items;
        if (items.Count == 0)
            return (BulkUpsertResponse<TaskIntentDto>)response.SetSuccess(Array.Empty<TaskIntentDto>());

        var memberOutcomes = await ServiceGuards.ResolveMembersForIntentAsync(
            _memberAuthorizationService, _dbContext, tenantId,
            items.Select(i => i.HouseholdMemberId).ToList(), cancellationToken);

        var planIds = items.Select(i => i.TaskPlanId).Distinct().ToList();
        var validPlanIds = (await _dbContext.TaskPlans
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && planIds.Contains(p.Id) && p.Template!.EventId == eventId)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        var memberIds = items.Select(i => i.HouseholdMemberId).Distinct().ToList();
        var existingByKey = (await _dbContext.TaskIntents
            .Where(i => i.TenantId == tenantId && planIds.Contains(i.TaskPlanId) && memberIds.Contains(i.HouseholdMemberId))
            .ToListAsync(cancellationToken))
            .ToDictionary(i => (i.TaskPlanId, i.HouseholdMemberId));

        var upserted = new List<TaskIntent>();
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            if (memberOutcomes.GetValueOrDefault(item.HouseholdMemberId) is string error)
            {
                response.ItemErrors.Add(new BulkItemError(index, error));
                continue;
            }

            if (!validPlanIds.Contains(item.TaskPlanId))
            {
                response.ItemErrors.Add(new BulkItemError(index, "Task plan not found."));
                continue;
            }

            if (!existingByKey.TryGetValue((item.TaskPlanId, item.HouseholdMemberId), out var existing))
            {
                existing = new TaskIntent
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    TaskPlanId = item.TaskPlanId,
                    HouseholdMemberId = item.HouseholdMemberId,
                };
                _dbContext.TaskIntents.Add(existing);
                existingByKey[(item.TaskPlanId, item.HouseholdMemberId)] = existing;
            }
            else if (existing.IsDeleted)
            {
                existing.IsDeleted = false;
            }

            existing.Volunteered = item.Volunteered;
            upserted.Add(existing);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        response.SetSuccess(upserted.Select(MapToDto).ToList());
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
