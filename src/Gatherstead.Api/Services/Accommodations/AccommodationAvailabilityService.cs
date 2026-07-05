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
        IReadOnlyCollection<Guid>? propertyIds = null,
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

        var query = _dbContext.Accommodations
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId);

        // Scope to the selected properties when any are given; an empty selection spans all properties.
        if (propertyIds is { Count: > 0 })
        {
            var propertyIdList = propertyIds.ToList();
            query = query.Where(a => propertyIdList.Contains(a.PropertyId));
        }

        var accommodations = await query
            .Select(a => new AccommodationRow(
                a.Id, a.PropertyId, a.Property!.Name, a.Name, a.Type, a.Notes,
                a.Beds.Select(b => new BedRow(b.Size, b.Quantity)).ToList()))
            .ToListAsync(cancellationToken);

        // Sum overlapping party sizes per accommodation. Two night spans overlap when each starts on
        // or before the other ends. Requested/Hold/Confirmed consume capacity; Declined stays do not.
        // The global query filter already excludes soft-deleted intents.
        var claims = await _dbContext.AccommodationIntents
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId
                && i.Status != AccommodationIntentStatus.Declined
                && i.StartNight <= endNight && i.EndNight >= startNight)
            .GroupBy(i => i.AccommodationId)
            .Select(g => new
            {
                AccommodationId = g.Key,
                Occupied = g.Sum(i => (i.PartyAdults ?? 0) + (i.PartyChildren ?? 0)),
            })
            .ToDictionaryAsync(c => c.AccommodationId, cancellationToken);

        var requestedParty = (partyAdults ?? 0) + (partyChildren ?? 0);

        var results = new List<AccommodationAvailabilityDto>(accommodations.Count);
        foreach (var a in accommodations)
        {
            claims.TryGetValue(a.Id, out var claim);
            var occupied = claim?.Occupied ?? 0;

            // Sleeps capacity summed from bed inventory; null when no beds are recorded (unconstrained).
            int? capacity = BedSizes.SleepsCapacity(a.Beds.Select(b => (b.Size, b.Quantity)));

            int? remaining = capacity.HasValue ? capacity.Value - occupied : null;

            // A null capacity is unconstrained, so it is always sufficient.
            var sufficient = !remaining.HasValue || remaining.Value >= requestedParty;

            if (requireCapacity && !sufficient)
                continue;

            results.Add(new AccommodationAvailabilityDto(
                a.Id, tenantId, a.PropertyId, a.PropertyName, a.Name, a.Type, a.Notes,
                capacity, occupied, remaining, sufficient));
        }

        // This search spans every property, so results group by property first, then by accommodation
        // type (canonical enum order) and name — matching the accommodation list ordering within each
        // property. Sorted in-memory because Name/PropertyName are Always Encrypted (PII) columns.
        var ordered = results
            .OrderBy(r => r.PropertyName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Type)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
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
        List<BedRow> Beds);

    private sealed record BedRow(BedSize Size, int Quantity);
}
