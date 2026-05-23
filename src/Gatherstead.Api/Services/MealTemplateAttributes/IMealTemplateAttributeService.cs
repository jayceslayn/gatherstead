using Gatherstead.Api.Contracts.MealTemplateAttributes;
using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Services.MealTemplateAttributes;

public interface IMealTemplateAttributeService
{
    Task<BaseEntityResponse<IReadOnlyCollection<MealTemplateAttributeDto>>> ListAsync(
        Guid tenantId,
        Guid mealTemplateId,
        IEnumerable<Guid>? ids = null,
        CancellationToken cancellationToken = default);

    Task<MealTemplateAttributeResponse> GetAsync(
        Guid tenantId,
        Guid mealTemplateId,
        Guid attributeId,
        CancellationToken cancellationToken = default);

    Task<MealTemplateAttributeResponse> CreateAsync(
        Guid tenantId,
        Guid mealTemplateId,
        CreateMealTemplateAttributeRequest request,
        CancellationToken cancellationToken = default);

    Task<MealTemplateAttributeResponse> UpdateAsync(
        Guid tenantId,
        Guid mealTemplateId,
        Guid attributeId,
        UpdateMealTemplateAttributeRequest request,
        CancellationToken cancellationToken = default);

    Task<MealTemplateAttributeResponse> DeleteAsync(
        Guid tenantId,
        Guid mealTemplateId,
        Guid attributeId,
        CancellationToken cancellationToken = default);
}
