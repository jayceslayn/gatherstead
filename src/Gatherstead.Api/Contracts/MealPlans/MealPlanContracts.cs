using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Contracts.MealPlans;

public record MealPlanDto(
    Guid Id,
    Guid TenantId,
    Guid MealTemplateId,
    DateOnly Day,
    MealType MealType,
    string? Notes,
    bool IsException,
    string? ExceptionReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool IsDeleted,
    DateTimeOffset? DeletedAt,
    Guid? DeletedByUserId);

public class MealPlanResponse : BaseEntityResponse<MealPlanDto> { }

public class UpdateMealPlanRequest
{
    public string? Notes { get; init; }
    public bool IsException { get; init; }
    public string? ExceptionReason { get; init; }
}
