using Gatherstead.Api.Contracts.MealTemplateAttributes;
using Gatherstead.Api.Services.Attributes;

namespace Gatherstead.Api.Services.MealTemplateAttributes;

public interface IMealTemplateAttributeService
    : IParentScopedAttributeService<MealTemplateAttributeDto, CreateMealTemplateAttributeRequest, UpdateMealTemplateAttributeRequest>
{
}
