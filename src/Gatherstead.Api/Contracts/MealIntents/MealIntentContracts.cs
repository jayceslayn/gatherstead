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

/// <summary>A member's volunteered cook sign-up enriched with its plan day, meal name and event
/// context, for the "My Upcoming Meals" dashboard widget and meal planner edit gating.</summary>
public record MyMealDto(
    Guid Id,
    Guid MealPlanId,
    Guid HouseholdMemberId,
    Guid TemplateId,
    string TemplateName,
    Guid EventId,
    string EventName,
    DateOnly Day,
    MealType MealType,
    string? Notes,
    IntentSource Source);

public class UpsertMealIntentRequest
{
    [Required]
    public Guid HouseholdMemberId { get; init; }
}
