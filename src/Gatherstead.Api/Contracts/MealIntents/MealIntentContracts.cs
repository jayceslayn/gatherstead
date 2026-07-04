using System;
using System.ComponentModel.DataAnnotations;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Contracts.MealIntents;

public record MealIntentDto(
    Guid Id,
    Guid TenantId,
    Guid MealPlanId,
    Guid HouseholdMemberId,
    IntentSource Source,
    AuditInfo? Audit);

public class MealIntentResponse : BaseEntityResponse<MealIntentDto> { }

public class UpsertMealIntentRequest
{
    [Required]
    public Guid HouseholdMemberId { get; init; }
}
