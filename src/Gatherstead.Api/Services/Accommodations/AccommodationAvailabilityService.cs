using Gatherstead.Api.Contracts.Accommodations;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Services.Validation;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Api.Services.Accommodations;

public class AccommodationAvailabilityService : IAccommodationAvailabilityService
{
    private readonly GathersteadDbContext _dbContext;
    private readonly ICurrentTenantContext _currentTenantContext;

    public AccommodationAvailabilityService(
        GathersteadDbContext dbContext,
        ICurrentTenantContext currentTenantContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentTenantContext = currentTenantContext ?? throw new ArgumentNullException(nameof(currentTenantContext));
    }

    public async Task<BaseEntityResponse<IReadOnlyCollection<AccommodationAvailabilityDto>>> SearchAsync(
        Guid tenantId,
        DateOnly startNight,
        DateOnly endNight,
        int? partyAdults,
        int? partyChildren,
        bool requireCapacity,
        CancellationToken cancellationToken = default)
    {
        var response = new BaseEntityResponse<IReadOnlyCollection<AccommodationAvailabilityDto>>();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        if (startNight > endNight)
        {
            response.AddResponseMessage(MessageType.ERROR, "StartNight must not be after EndNight.");
            return response;
        }

        var accommodations = await _dbContext.Accommodations
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId)
            .Select(a => new AccommodationRow(
                a.Id, a.PropertyId, a.Property!.Name, a.Name, a.Type, a.Notes, a.CapacityAdults, a.CapacityChildren))
            .ToListAsync(cancellationToken);

        // Sum overlapping party sizes per accommodation. Two night spans overlap when each starts on
        // or before the other ends. Intent/Hold/Confirmed all consume capacity; the global query
        // filter already excludes soft-deleted intents.
        var claims = await _dbContext.AccommodationIntents
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId && i.StartNight <= endNight && i.EndNight >= startNight)
            .GroupBy(i => i.AccommodationId)
            .Select(g => new
            {
                AccommodationId = g.Key,
                ClaimedAdults = g.Sum(i => i.PartyAdults ?? 0),
                ClaimedChildren = g.Sum(i => i.PartyChildren ?? 0),
            })
            .ToDictionaryAsync(c => c.AccommodationId, cancellationToken);

        var requestedAdults = partyAdults ?? 0;
        var requestedChildren = partyChildren ?? 0;

        var results = new List<AccommodationAvailabilityDto>(accommodations.Count);
        foreach (var a in accommodations)
        {
            claims.TryGetValue(a.Id, out var claim);
            var claimedAdults = claim?.ClaimedAdults ?? 0;
            var claimedChildren = claim?.ClaimedChildren ?? 0;

            int? remainingAdults = a.CapacityAdults.HasValue ? a.CapacityAdults.Value - claimedAdults : null;
            int? remainingChildren = a.CapacityChildren.HasValue ? a.CapacityChildren.Value - claimedChildren : null;

            // A null capacity dimension is unconstrained, so it is always sufficient.
            var adultsOk = !remainingAdults.HasValue || remainingAdults.Value >= requestedAdults;
            var childrenOk = !remainingChildren.HasValue || remainingChildren.Value >= requestedChildren;
            var sufficient = adultsOk && childrenOk;

            if (requireCapacity && !sufficient)
                continue;

            results.Add(new AccommodationAvailabilityDto(
                a.Id, tenantId, a.PropertyId, a.PropertyName, a.Name, a.Type, a.Notes,
                a.CapacityAdults, a.CapacityChildren,
                claimedAdults, claimedChildren,
                remainingAdults, remainingChildren,
                sufficient));
        }

        // Sufficient options first, then a stable property/accommodation ordering.
        var ordered = results
            .OrderByDescending(r => r.HasSufficientCapacity)
            .ThenBy(r => r.PropertyName)
            .ThenBy(r => r.Name)
            .ToList();

        return BaseEntityResponse<IReadOnlyCollection<AccommodationAvailabilityDto>>.SuccessfulResponse(ordered);
    }

    private sealed record AccommodationRow(
        Guid Id,
        Guid PropertyId,
        string PropertyName,
        string Name,
        AccommodationType Type,
        string? Notes,
        int? CapacityAdults,
        int? CapacityChildren);
}
