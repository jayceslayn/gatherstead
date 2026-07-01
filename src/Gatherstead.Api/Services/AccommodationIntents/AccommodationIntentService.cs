using Gatherstead.Api.Contracts.AccommodationIntents;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.Validation;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Api.Services.AccommodationIntents;

public class AccommodationIntentService : IAccommodationIntentService
{
    private const string EntityDisplayName = "Accommodation intent";

    private readonly GathersteadDbContext _dbContext;
    private readonly ICurrentTenantContext _currentTenantContext;
    private readonly IMemberAuthorizationService _memberAuthorizationService;
    private readonly IAuditVisibilityContext _auditVisibility;

    public AccommodationIntentService(
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

    public async Task<BaseEntityResponse<IReadOnlyCollection<AccommodationIntentDto>>> ListAsync(
        Guid tenantId,
        Guid accommodationId,
        IEnumerable<Guid>? memberIds = null,
        CancellationToken cancellationToken = default)
    {
        var response = new BaseEntityResponse<IReadOnlyCollection<AccommodationIntentDto>>();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        var query = _dbContext.AccommodationIntents
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId && i.AccommodationId == accommodationId);

        if (memberIds is not null)
        {
            var memberIdList = memberIds.ToList();
            if (memberIdList.Count > 0)
                query = query.Where(i => memberIdList.Contains(i.HouseholdMemberId));
        }

        var intents = await query.Select(i => MapToDto(i)).ToListAsync(cancellationToken);

        return BaseEntityResponse<IReadOnlyCollection<AccommodationIntentDto>>.SuccessfulResponse(intents);
    }

    public async Task<BaseEntityResponse<IReadOnlyCollection<MyStayDto>>> ListForTenantAsync(
        Guid tenantId,
        IEnumerable<Guid>? memberIds = null,
        DateOnly? fromNight = null,
        CancellationToken cancellationToken = default)
    {
        var response = new BaseEntityResponse<IReadOnlyCollection<MyStayDto>>();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        var query = _dbContext.AccommodationIntents
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId);

        if (memberIds is not null)
        {
            var memberIdList = memberIds.ToList();
            if (memberIdList.Count > 0)
                query = query.Where(i => memberIdList.Contains(i.HouseholdMemberId));
        }

        if (fromNight is DateOnly from)
            query = query.Where(i => i.EndNight >= from);

        var stays = await query
            .OrderBy(i => i.StartNight)
            .ThenBy(i => i.EndNight)
            .Select(i => new MyStayDto(
                i.Id,
                i.AccommodationId,
                i.Accommodation!.Name,
                i.Accommodation.PropertyId,
                i.Accommodation.Property!.Name,
                i.HouseholdMemberId,
                i.StartNight,
                i.EndNight,
                i.Status,
                i.Decision,
                i.PartyAdults,
                i.PartyChildren))
            .ToListAsync(cancellationToken);

        return BaseEntityResponse<IReadOnlyCollection<MyStayDto>>.SuccessfulResponse(stays);
    }

    public async Task<AccommodationIntentResponse> GetAsync(
        Guid tenantId,
        Guid accommodationId,
        Guid intentId,
        CancellationToken cancellationToken = default)
    {
        var response = new AccommodationIntentResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        var intent = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.AccommodationIntents.AsNoTracking()
                .Where(i => i.TenantId == tenantId && i.AccommodationId == accommodationId && i.Id == intentId),
            EntityDisplayName,
            cancellationToken);

        if (intent is null) return response;

        response.SetSuccess(MapToDto(intent));
        return response;
    }

    public async Task<AccommodationIntentResponse> CreateAsync(
        Guid tenantId,
        Guid accommodationId,
        Guid householdId,
        CreateAccommodationIntentRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new AccommodationIntentResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!ServiceGuards.RequireRequest(request, "create accommodation intent", response))
            return response;
        if (!await ServiceGuards.AuthorizeIntentAssignAsync(response, _memberAuthorizationService, tenantId, householdId, request.HouseholdMemberId, cancellationToken))
            return response;
        if (!await ServiceGuards.RequireMemberExistsAsync(response, _dbContext, tenantId, householdId, request.HouseholdMemberId, cancellationToken))
            return response;

        var accommodationExists = await _dbContext.Accommodations
            .AsNoTracking()
            .AnyAsync(a => a.TenantId == tenantId && a.Id == accommodationId, cancellationToken);

        if (!accommodationExists)
        {
            response.AddResponseMessage(MessageType.ERROR, "Accommodation not found.");
            return response;
        }

        // A stay is a span of nights; capacity is a soft UI signal, so overlapping stays in the
        // same accommodation are intentionally allowed (families may share, and over-capacity is
        // only flagged in the UI). The only structural rule is a non-inverted span.
        if (!ValidateSpan(request.StartNight, request.EndNight, response))
            return response;

        var intent = new AccommodationIntent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AccommodationId = accommodationId,
            HouseholdMemberId = request.HouseholdMemberId,
            StartNight = request.StartNight,
            EndNight = request.EndNight,
            Status = request.Status,
            Notes = request.Notes?.Trim(),
            PartyAdults = request.PartyAdults,
            PartyChildren = request.PartyChildren,
            Priority = request.Priority,
        };

        _dbContext.AccommodationIntents.Add(intent);
        await _dbContext.SaveChangesAsync(cancellationToken);

        response.SetSuccess(MapToDto(intent));
        return response;
    }

    public async Task<AccommodationIntentResponse> UpdateAsync(
        Guid tenantId,
        Guid accommodationId,
        Guid intentId,
        UpdateAccommodationIntentRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new AccommodationIntentResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!ServiceGuards.RequireRequest(request, "update accommodation intent", response))
            return response;

        var intent = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.AccommodationIntents
                .Where(i => i.TenantId == tenantId && i.AccommodationId == accommodationId && i.Id == intentId),
            EntityDisplayName,
            cancellationToken);

        if (intent is null) return response;

        // Authorize against the (possibly new) member the stay is being assigned to.
        if (!await ServiceGuards.AuthorizeIntentAssignAsync(response, _memberAuthorizationService, tenantId, Guid.Empty, request.HouseholdMemberId, cancellationToken))
            return response;

        if (!ValidateSpan(request.StartNight, request.EndNight, response))
            return response;

        // Moving the stay to a different accommodation — verify the target exists for the tenant.
        if (request.AccommodationId != accommodationId)
        {
            var targetExists = await _dbContext.Accommodations
                .AsNoTracking()
                .AnyAsync(a => a.TenantId == tenantId && a.Id == request.AccommodationId, cancellationToken);

            if (!targetExists)
            {
                response.AddResponseMessage(MessageType.ERROR, "Accommodation not found.");
                return response;
            }

            intent.AccommodationId = request.AccommodationId;
        }

        intent.HouseholdMemberId = request.HouseholdMemberId;
        intent.StartNight = request.StartNight;
        intent.EndNight = request.EndNight;
        intent.Status = request.Status;
        intent.Notes = request.Notes?.Trim();
        intent.Decision = request.Decision;
        intent.PartyAdults = request.PartyAdults;
        intent.PartyChildren = request.PartyChildren;
        intent.Priority = request.Priority;

        await _dbContext.SaveChangesAsync(cancellationToken);

        response.SetSuccess(MapToDto(intent));
        return response;
    }

    public async Task<AccommodationIntentResponse> DeleteAsync(
        Guid tenantId,
        Guid accommodationId,
        Guid intentId,
        CancellationToken cancellationToken = default)
    {
        var response = new AccommodationIntentResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        var intent = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.AccommodationIntents
                .Where(i => i.TenantId == tenantId && i.AccommodationId == accommodationId && i.Id == intentId),
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

    private AccommodationIntentDto MapToDto(AccommodationIntent i) => new(
        i.Id, i.TenantId, i.AccommodationId, i.HouseholdMemberId, i.StartNight, i.EndNight, i.Status, i.Notes,
        i.Decision, i.PartyAdults, i.PartyChildren, i.Priority,
        i.ToAuditInfo(_auditVisibility.IncludeAudit));

    private static bool ValidateSpan(DateOnly startNight, DateOnly endNight, AccommodationIntentResponse response)
    {
        if (startNight > endNight)
        {
            response.AddResponseMessage(MessageType.ERROR, "StartNight must not be after EndNight.");
            return false;
        }

        return true;
    }
}
