using Gatherstead.Api.Contracts.MealIntents;
using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Services.MealIntents;

public interface IMealIntentService
{
    Task<BaseEntityResponse<IReadOnlyCollection<MealIntentDto>>> ListAsync(Guid tenantId, Guid planId, IEnumerable<Guid>? memberIds = null, CancellationToken cancellationToken = default);
    Task<MealIntentResponse> GetAsync(Guid tenantId, Guid planId, Guid intentId, CancellationToken cancellationToken = default);
    Task<MealIntentResponse> UpsertAsync(Guid tenantId, Guid planId, Guid householdId, UpsertMealIntentRequest request, CancellationToken cancellationToken = default);
    Task<MealIntentResponse> DeleteAsync(Guid tenantId, Guid planId, Guid intentId, CancellationToken cancellationToken = default);
}
