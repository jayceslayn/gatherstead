using System;
using System.ComponentModel.DataAnnotations;
using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Contracts.MealIntents;

public record MealIntentDto(
    Guid Id,
    Guid TenantId,
    Guid MealPlanId,
    Guid HouseholdMemberId,
    bool Volunteered,
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

    public bool Volunteered { get; init; }
}
