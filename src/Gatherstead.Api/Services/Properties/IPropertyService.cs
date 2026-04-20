using Gatherstead.Api.Contracts.Properties;
using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Services.Properties;

public interface IPropertyService
{
    Task<BaseEntityResponse<IReadOnlyCollection<PropertyDto>>> ListAsync(Guid tenantId, IEnumerable<Guid>? ids = null, CancellationToken cancellationToken = default);
    Task<PropertyResponse> GetAsync(Guid tenantId, Guid propertyId, CancellationToken cancellationToken = default);
    Task<PropertyResponse> CreateAsync(Guid tenantId, CreatePropertyRequest request, CancellationToken cancellationToken = default);
    Task<PropertyResponse> UpdateAsync(Guid tenantId, Guid propertyId, UpdatePropertyRequest request, CancellationToken cancellationToken = default);
    Task<PropertyResponse> DeleteAsync(Guid tenantId, Guid propertyId, CancellationToken cancellationToken = default);
}
