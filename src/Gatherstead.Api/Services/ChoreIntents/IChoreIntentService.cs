using Gatherstead.Api.Contracts.ChoreIntents;
using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Services.ChoreIntents;

public interface IChoreIntentService
{
    Task<BaseEntityResponse<IReadOnlyCollection<ChoreIntentDto>>> ListAsync(Guid tenantId, Guid planId, IEnumerable<Guid>? memberIds = null, CancellationToken cancellationToken = default);
    Task<ChoreIntentResponse> GetAsync(Guid tenantId, Guid planId, Guid intentId, CancellationToken cancellationToken = default);
    Task<ChoreIntentResponse> UpsertAsync(Guid tenantId, Guid planId, Guid householdId, UpsertChoreIntentRequest request, CancellationToken cancellationToken = default);
    Task<ChoreIntentResponse> DeleteAsync(Guid tenantId, Guid planId, Guid intentId, CancellationToken cancellationToken = default);
}
