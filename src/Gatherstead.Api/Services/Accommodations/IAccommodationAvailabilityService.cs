using Gatherstead.Api.Contracts.Accommodations;
using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Services.Accommodations;

public interface IAccommodationAvailabilityService
{
    /// <summary>
    /// Lists tenant accommodations with their remaining capacity for the requested night span.
    /// When <paramref name="requireCapacity"/> is true, only accommodations that can fit the
    /// requested party (for both adults and children) are returned; otherwise all are returned with
    /// a <see cref="AccommodationAvailabilityDto.HasSufficientCapacity"/> flag.
    /// </summary>
    Task<BaseEntityResponse<IReadOnlyCollection<AccommodationAvailabilityDto>>> SearchAsync(
        Guid tenantId,
        DateOnly startNight,
        DateOnly endNight,
        int? partyAdults,
        int? partyChildren,
        bool requireCapacity,
        CancellationToken cancellationToken = default);
}
