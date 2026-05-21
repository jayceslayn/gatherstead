using Gatherstead.Api.Contracts.TaskIntents;
using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Services.TaskIntents;

public interface ITaskIntentService
{
    Task<BaseEntityResponse<IReadOnlyCollection<TaskIntentDto>>> ListAsync(Guid tenantId, Guid planId, IEnumerable<Guid>? memberIds = null, CancellationToken cancellationToken = default);
    Task<TaskIntentResponse> GetAsync(Guid tenantId, Guid planId, Guid intentId, CancellationToken cancellationToken = default);
    Task<TaskIntentResponse> UpsertAsync(Guid tenantId, Guid planId, Guid householdId, UpsertTaskIntentRequest request, CancellationToken cancellationToken = default);
    Task<TaskIntentResponse> DeleteAsync(Guid tenantId, Guid planId, Guid intentId, CancellationToken cancellationToken = default);
}
