using System.ComponentModel.DataAnnotations;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Contracts.MealIntents;

public record MealIntentDto(
    Guid Id,
    Guid TenantId,
    Guid MealPlanId,
    Guid HouseholdMemberId,
    MealIntentStatus Status,
    bool BringOwnFood,
    string? Notes,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool IsDeleted,
    DateTimeOffset? DeletedAt,
    Guid? DeletedByUserId);

public class MealIntentResponse : BaseEntityResponse<MealIntentDto> { }

public class UpsertMealIntentRequest
{
    [Required]
    public Guid HouseholdMemberId { get; init; }

    [Required]
    public MealIntentStatus Status { get; init; }

    public bool BringOwnFood { get; init; }
    public string? Notes { get; init; }
}
