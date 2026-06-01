using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Contracts.Reports;

/// <summary>
/// Aggregated, read-only view of an event's attendance and meals, surfacing per-day headcounts
/// and per-meal dietary needs so cooks know how much and what kind of food to prepare.
/// </summary>
public record EventReportDto(
    Guid EventId,
    string EventName,
    DateOnly StartDate,
    DateOnly EndDate,
    IReadOnlyList<EventReportDayDto> Days);

public record EventReportDayDto(
    DateOnly Day,
    int Going,
    int Maybe,
    IReadOnlyList<EventReportMealDto> Meals);

public record EventReportMealDto(
    Guid MealPlanId,
    string TemplateName,
    MealType MealType,
    int Going,
    int Maybe,
    int NotGoing,
    int BringOwnFood,
    IReadOnlyList<DietaryTallyDto> Dietary,
    IReadOnlyList<EventReportAttendeeDto> Attendees);

public record DietaryTallyDto(string Label, int Count);

public record EventReportAttendeeDto(
    Guid MemberId,
    string Name,
    AttendanceStatus Status,
    bool BringOwnFood,
    IReadOnlyList<string> Dietary,
    string? DietaryNotes);

public class EventReportResponse : BaseEntityResponse<EventReportDto> { }
