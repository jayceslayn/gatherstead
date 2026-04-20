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

    public AccommodationIntentService(
        GathersteadDbContext dbContext,
        ICurrentTenantContext currentTenantContext,
        IMemberAuthorizationService memberAuthorizationService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentTenantContext = currentTenantContext ?? throw new ArgumentNullException(nameof(currentTenantContext));
        _memberAuthorizationService = memberAuthorizationService ?? throw new ArgumentNullException(nameof(memberAuthorizationService));
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
        if (!await ServiceGuards.AuthorizeMemberEditAsync(response, _memberAuthorizationService, tenantId, householdId, request.HouseholdMemberId, cancellationToken))
            return response;

        var accommodationExists = await _dbContext.Accommodations
            .AsNoTracking()
            .AnyAsync(a => a.TenantId == tenantId && a.Id == accommodationId, cancellationToken);

        if (!accommodationExists)
        {
            response.AddResponseMessage(MessageType.ERROR, "Accommodation not found.");
            return response;
        }

        var duplicateExists = await _dbContext.AccommodationIntents
            .AsNoTracking()
            .AnyAsync(i => i.TenantId == tenantId && i.AccommodationId == accommodationId && i.HouseholdMemberId == request.HouseholdMemberId && i.Night == request.Night, cancellationToken);

        if (duplicateExists)
        {
            response.AddResponseMessage(MessageType.ERROR, "An accommodation intent for this member and night already exists.");
            return response;
        }

        var intent = new AccommodationIntent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AccommodationId = accommodationId,
            HouseholdMemberId = request.HouseholdMemberId,
            Night = request.Night,
            Status = request.Status,
            Notes = request.Notes?.Trim(),
            PartySize = request.PartySize,
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

        if (!await ServiceGuards.AuthorizeMemberEditAsync(response, _memberAuthorizationService, tenantId, Guid.Empty, intent.HouseholdMemberId, cancellationToken))
            return response;

        intent.Status = request.Status;
        intent.Notes = request.Notes?.Trim();
        intent.Decision = request.Decision;
        intent.PartySize = request.PartySize;
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

    private static AccommodationIntentDto MapToDto(AccommodationIntent i) => new(
        i.Id, i.TenantId, i.AccommodationId, i.HouseholdMemberId, i.Night, i.Status, i.Notes,
        i.Decision, i.PartySize, i.Priority,
        i.CreatedAt, i.UpdatedAt, i.IsDeleted, i.DeletedAt, i.DeletedByUserId);
}
