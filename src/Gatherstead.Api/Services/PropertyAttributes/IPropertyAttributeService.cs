using Gatherstead.Api.Contracts.PropertyAttributes;
using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Services.PropertyAttributes;

public interface IPropertyAttributeService
{
    Task<BaseEntityResponse<IReadOnlyCollection<PropertyAttributeDto>>> ListAsync(
        Guid tenantId,
        Guid propertyId,
        IEnumerable<Guid>? ids = null,
        CancellationToken cancellationToken = default);

    Task<PropertyAttributeResponse> GetAsync(
        Guid tenantId,
        Guid propertyId,
        Guid attributeId,
        CancellationToken cancellationToken = default);

    Task<PropertyAttributeResponse> CreateAsync(
        Guid tenantId,
        Guid propertyId,
        CreatePropertyAttributeRequest request,
        CancellationToken cancellationToken = default);

    Task<PropertyAttributeResponse> UpdateAsync(
        Guid tenantId,
        Guid propertyId,
        Guid attributeId,
        UpdatePropertyAttributeRequest request,
        CancellationToken cancellationToken = default);

    Task<PropertyAttributeResponse> DeleteAsync(
        Guid tenantId,
        Guid propertyId,
        Guid attributeId,
        CancellationToken cancellationToken = default);
}
