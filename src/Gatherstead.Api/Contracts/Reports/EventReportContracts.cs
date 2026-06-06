using System.ComponentModel.DataAnnotations;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Contracts.Reports;

/// <summary>
/// Aggregated, read-only view of an event's attendance and meals, surfacing per-day headcounts
/// and per-meal dietary needs so cooks know how much and what kind of food to prepare.
/// </summary>
public record EventReportDto(
    [property: Required] Guid EventId,
    [property: Required] string EventName,
    [property: Required] DateOnly StartDate,
    [property: Required] DateOnly EndDate,
    [property: Required] IReadOnlyList<EventReportDayDto> Days);

public record EventReportDayDto(
    [property: Required] DateOnly Day,
    [property: Required] int Going,
    [property: Required] int Maybe,
    [property: Required] IReadOnlyList<EventReportMealDto> Meals);

public record EventReportMealDto(
    [property: Required] Guid MealPlanId,
    [property: Required] string TemplateName,
    [property: Required] MealType MealType,
    [property: Required] int Going,
    [property: Required] int Maybe,
    [property: Required] int NotGoing,
    [property: Required] int BringOwnFood,
    [property: Required] IReadOnlyList<DietaryTallyDto> Dietary,
    [property: Required] IReadOnlyList<EventReportAttendeeDto> Attendees);

public record DietaryTallyDto(
    [property: Required] string Label,
    [property: Required] int Count);

public record EventReportAttendeeDto(
    [property: Required] Guid MemberId,
    [property: Required] string Name,
    [property: Required] AttendanceStatus Status,
    [property: Required] bool BringOwnFood,
    [property: Required] IReadOnlyList<string> Dietary,
    string? DietaryNotes);

public class EventReportResponse : BaseEntityResponse<EventReportDto> { }
