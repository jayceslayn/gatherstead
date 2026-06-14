using Gatherstead.Api.Contracts.TaskPlans;
using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Services.TaskPlans;

public interface ITaskPlanService
{
    Task<BaseEntityResponse<IReadOnlyCollection<TaskPlanDto>>> ListAsync(Guid tenantId, Guid eventId, Guid templateId, IEnumerable<Guid>? ids = null, CancellationToken cancellationToken = default);
    Task<TaskPlanResponse> GetAsync(Guid tenantId, Guid templateId, Guid planId, CancellationToken cancellationToken = default);
    Task<TaskPlanResponse> UpdateAsync(Guid tenantId, Guid templateId, Guid planId, UpdateTaskPlanRequest request, CancellationToken cancellationToken = default);
    Task<TaskPlanResponse> DeleteAsync(Guid tenantId, Guid templateId, Guid planId, CancellationToken cancellationToken = default);
}
