using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Contracts.Tenants;

namespace Gatherstead.Api.Services.Tenants;

public interface ITenantService
{
    Task<BaseEntityResponse<IReadOnlyCollection<TenantSummary>>> ListAsync(Guid userId, IEnumerable<Guid>? ids = null, CancellationToken cancellationToken = default);
    Task<TenantResponse> GetAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<TenantResponse> CreateAsync(CreateTenantRequest request, CancellationToken cancellationToken = default);
    Task<TenantResponse> UpdateAsync(Guid tenantId, UpdateTenantRequest request, CancellationToken cancellationToken = default);
    Task<TenantResponse> DeleteAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
