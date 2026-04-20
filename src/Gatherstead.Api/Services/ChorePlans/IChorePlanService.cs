using Gatherstead.Api.Contracts.ChorePlans;
using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Services.ChorePlans;

public interface IChorePlanService
{
    Task<BaseEntityResponse<IReadOnlyCollection<ChorePlanDto>>> ListAsync(Guid tenantId, Guid eventId, Guid templateId, IEnumerable<Guid>? ids = null, CancellationToken cancellationToken = default);
    Task<ChorePlanResponse> GetAsync(Guid tenantId, Guid templateId, Guid planId, CancellationToken cancellationToken = default);
    Task<ChorePlanResponse> UpdateAsync(Guid tenantId, Guid templateId, Guid planId, UpdateChorePlanRequest request, CancellationToken cancellationToken = default);
}
