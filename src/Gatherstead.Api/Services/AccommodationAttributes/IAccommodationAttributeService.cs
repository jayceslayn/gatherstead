using Gatherstead.Api.Contracts.AccommodationAttributes;
using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Services.AccommodationAttributes;

public interface IAccommodationAttributeService
{
    Task<BaseEntityResponse<IReadOnlyCollection<AccommodationAttributeDto>>> ListAsync(
        Guid tenantId,
        Guid accommodationId,
        IEnumerable<Guid>? ids = null,
        CancellationToken cancellationToken = default);

    Task<AccommodationAttributeResponse> GetAsync(
        Guid tenantId,
        Guid accommodationId,
        Guid attributeId,
        CancellationToken cancellationToken = default);

    Task<AccommodationAttributeResponse> CreateAsync(
        Guid tenantId,
        Guid accommodationId,
        CreateAccommodationAttributeRequest request,
        CancellationToken cancellationToken = default);

    Task<AccommodationAttributeResponse> UpdateAsync(
        Guid tenantId,
        Guid accommodationId,
        Guid attributeId,
        UpdateAccommodationAttributeRequest request,
        CancellationToken cancellationToken = default);

    Task<AccommodationAttributeResponse> DeleteAsync(
        Guid tenantId,
        Guid accommodationId,
        Guid attributeId,
        CancellationToken cancellationToken = default);
}
