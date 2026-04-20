using Gatherstead.Api.Contracts.Accommodations;
using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Services.Accommodations;

public interface IAccommodationService
{
    Task<BaseEntityResponse<IReadOnlyCollection<AccommodationDto>>> ListAsync(Guid tenantId, Guid propertyId, IEnumerable<Guid>? ids = null, CancellationToken cancellationToken = default);
    Task<AccommodationResponse> GetAsync(Guid tenantId, Guid propertyId, Guid accommodationId, CancellationToken cancellationToken = default);
    Task<AccommodationResponse> CreateAsync(Guid tenantId, Guid propertyId, CreateAccommodationRequest request, CancellationToken cancellationToken = default);
    Task<AccommodationResponse> UpdateAsync(Guid tenantId, Guid propertyId, Guid accommodationId, UpdateAccommodationRequest request, CancellationToken cancellationToken = default);
    Task<AccommodationResponse> DeleteAsync(Guid tenantId, Guid propertyId, Guid accommodationId, CancellationToken cancellationToken = default);
}
