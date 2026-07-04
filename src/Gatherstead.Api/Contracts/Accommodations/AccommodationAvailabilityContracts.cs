using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Contracts.Accommodations;

/// <summary>
/// One accommodation's availability for a requested date span. Capacity is a soft signal: overlapping
/// stays are intentionally permitted, so <see cref="Remaining"/> can be negative when over-claimed.
/// <see cref="Capacity"/> is the total sleeps derived from the bed inventory; a null capacity is
/// unconstrained (always sufficient). <see cref="Occupied"/> sums the party size of overlapping,
/// non-declined stays.
/// </summary>
public record AccommodationAvailabilityDto(
    Guid Id,
    Guid TenantId,
    Guid PropertyId,
    string PropertyName,
    string Name,
    AccommodationType Type,
    string? Notes,
    int? Capacity,
    int Occupied,
    int? Remaining,
    bool HasSufficientCapacity);
