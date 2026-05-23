using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Contracts.TenantAttributes;

namespace Gatherstead.Api.Services.TenantAttributes;

public interface ITenantAttributeService
{
    Task<BaseEntityResponse<IReadOnlyCollection<TenantAttributeDto>>> ListAsync(
        Guid tenantId,
        IEnumerable<Guid>? ids = null,
        CancellationToken cancellationToken = default);

    Task<TenantAttributeResponse> GetAsync(
        Guid tenantId,
        Guid attributeId,
        CancellationToken cancellationToken = default);

    Task<TenantAttributeResponse> CreateAsync(
        Guid tenantId,
        CreateTenantAttributeRequest request,
        CancellationToken cancellationToken = default);

    Task<TenantAttributeResponse> UpdateAsync(
        Guid tenantId,
        Guid attributeId,
        UpdateTenantAttributeRequest request,
        CancellationToken cancellationToken = default);

    Task<TenantAttributeResponse> DeleteAsync(
        Guid tenantId,
        Guid attributeId,
        CancellationToken cancellationToken = default);
}
