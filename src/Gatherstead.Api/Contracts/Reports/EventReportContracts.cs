using System.ComponentModel.DataAnnotations;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Contracts.Reports;

/// <summary>
/// Aggregated, read-only view of an event's attendance, meals, task coverage, and accommodation
/// occupancy, surfacing per-day headcounts and per-meal dietary needs so cooks know how much and
/// what kind of food to prepare, plus which task plans are uncovered and where people are staying.
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
    [property: Required] IReadOnlyList<EventReportDayAttendeeDto> Attendees,
    [property: Required] IReadOnlyList<EventReportMealDto> Meals,
    [property: Required] IReadOnlyList<EventReportTaskDto> Tasks,
    [property: Required] IReadOnlyList<EventReportAccommodationDto> Accommodations);

/// <summary>
/// A member attending (or maybe attending) the event on a given day. Household id/name are
/// included so the report can group each day's attendees by household.
/// </summary>
public record EventReportDayAttendeeDto(
    [property: Required] Guid MemberId,
    [property: Required] string Name,
    [property: Required] AttendanceStatus Status,
    [property: Required] Guid HouseholdId,
    [property: Required] string HouseholdName);

/// <summary>
/// A task plan for a given day/slot with its assignee count. Coverage status (covered / partial /
/// open) is a UI threshold derived client-side from <see cref="AssigneeCount"/> versus
/// <see cref="MinimumAssignees"/>, so it is intentionally not computed here.
/// </summary>
public record EventReportTaskDto(
    [property: Required] Guid TaskPlanId,
    [property: Required] Guid TemplateId,
    [property: Required] string TemplateName,
    TaskTimeSlot? TimeSlot,
    [property: Required] int AssigneeCount,
    int? MinimumAssignees,
    [property: Required] bool Completed,
    [property: Required] bool IsException,
    string? ExceptionReason,
    [property: Required] IReadOnlyList<string> Assignees);

/// <summary>
/// An accommodation and who is staying in it on a given night. Emitted for every accommodation on
/// every event day (a vacant night reports <see cref="Occupied"/> 0 with no occupants) so the
/// occupancy badge renders on all days. Vacant / full / over is derived client-side from
/// <see cref="Occupied"/> versus capacity; over-capacity is a soft flag, never blocked.
/// </summary>
public record EventReportAccommodationDto(
    [property: Required] Guid AccommodationId,
    [property: Required] string Name,
    [property: Required] AccommodationType Type,
    /// <summary>Total sleeps derived from the bed inventory; null when no beds are recorded.</summary>
    int? Capacity,
    string? Notes,
    [property: Required] int Occupied,
    [property: Required] IReadOnlyList<EventReportOccupantDto> Occupants);

public record EventReportOccupantDto(
    [property: Required] Guid MemberId,
    [property: Required] string Name,
    [property: Required] AccommodationIntentStatus Status,
    int? PartyAdults,
    int? PartyChildren);

public record EventReportMealDto(
    [property: Required] Guid MealPlanId,
    [property: Required] Guid TemplateId,
    [property: Required] string TemplateName,
    [property: Required] MealType MealType,
    [property: Required] bool IsException,
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
