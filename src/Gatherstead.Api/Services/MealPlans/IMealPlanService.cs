using Gatherstead.Api.Contracts.MealPlans;
using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Services.MealPlans;

public interface IMealPlanService
{
    Task<BaseEntityResponse<IReadOnlyCollection<MealPlanDto>>> ListAsync(Guid tenantId, Guid eventId, Guid templateId, IEnumerable<Guid>? ids = null, CancellationToken cancellationToken = default);
    Task<MealPlanResponse> GetAsync(Guid tenantId, Guid templateId, Guid planId, CancellationToken cancellationToken = default);
    Task<MealPlanResponse> UpdateAsync(Guid tenantId, Guid templateId, Guid planId, UpdateMealPlanRequest request, CancellationToken cancellationToken = default);
}
