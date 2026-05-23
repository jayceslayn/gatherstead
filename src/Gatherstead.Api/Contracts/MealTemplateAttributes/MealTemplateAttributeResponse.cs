using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Contracts.MealTemplateAttributes;

public class MealTemplateAttributeResponse : BaseEntityResponse<MealTemplateAttributeDto>
{
}

public record MealTemplateAttributeDto(
    Guid Id,
    Guid TenantId,
    Guid MealTemplateId,
    string Key,
    string Value,
    byte TenantMinRole,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool IsDeleted,
    DateTimeOffset? DeletedAt,
    Guid? DeletedByUserId);
