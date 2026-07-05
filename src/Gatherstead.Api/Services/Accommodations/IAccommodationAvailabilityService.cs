using Gatherstead.Api.Contracts.Accommodations;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Services.Accommodations;

public interface IAccommodationAvailabilityService
{
    /// <summary>
    /// Lists tenant accommodations with their remaining capacity for the requested night span.
    /// When <paramref name="requireCapacity"/> is true, only accommodations that can fit the
    /// requested party (for both adults and children) are returned; otherwise all are returned with
    /// a <see cref="AccommodationAvailabilityDto.HasSufficientCapacity"/> flag. When
    /// <paramref name="propertyIds"/> is non-empty the search is scoped to those properties;
    /// a null or empty collection searches every property in the tenant. Likewise, when
    /// <paramref name="types"/> is non-empty the search is scoped to those accommodation types;
    /// a null or empty collection spans every type.
    /// </summary>
    Task<BaseEntityResponse<IReadOnlyCollection<AccommodationAvailabilityDto>>> SearchAsync(
        Guid tenantId,
        DateOnly startNight,
        DateOnly endNight,
        int? partyAdults,
        int? partyChildren,
        bool requireCapacity,
        IReadOnlyCollection<Guid>? propertyIds = null,
        IReadOnlyCollection<AccommodationType>? types = null,
        CancellationToken cancellationToken = default);
}
