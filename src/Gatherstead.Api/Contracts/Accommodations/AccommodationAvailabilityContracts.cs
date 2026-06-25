using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Contracts.Accommodations;

/// <summary>
/// One accommodation's availability for a requested date span. Capacity is a soft signal: overlapping
/// stays are intentionally permitted, so <see cref="RemainingAdults"/>/<see cref="RemainingChildren"/>
/// can be negative when over-claimed. A null capacity dimension is unconstrained (always sufficient).
/// </summary>
public record AccommodationAvailabilityDto(
    Guid Id,
    Guid TenantId,
    Guid PropertyId,
    string PropertyName,
    string Name,
    AccommodationType Type,
    string? Notes,
    int? CapacityAdults,
    int? CapacityChildren,
    int ClaimedAdults,
    int ClaimedChildren,
    int? RemainingAdults,
    int? RemainingChildren,
    bool HasSufficientCapacity);
