using System.ComponentModel.DataAnnotations;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Contracts.EventAttendance;

public record EventAttendanceDto(
    Guid Id,
    Guid TenantId,
    Guid EventId,
    Guid HouseholdMemberId,
    DateOnly Day,
    AttendanceStatus Status,
    DateTimeOffset? ArrivalWindowStart,
    DateTimeOffset? ArrivalWindowEnd,
    DateTimeOffset? DepartureWindowStart,
    DateTimeOffset? DepartureWindowEnd,
    string? Notes,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool IsDeleted,
    DateTimeOffset? DeletedAt,
    Guid? DeletedByUserId);

public class EventAttendanceResponse : BaseEntityResponse<EventAttendanceDto> { }

public class UpsertEventAttendanceRequest
{
    [Required]
    public Guid HouseholdMemberId { get; init; }

    [Required]
    public DateOnly Day { get; init; }

    [Required]
    public AttendanceStatus Status { get; init; }

    public DateTimeOffset? ArrivalWindowStart { get; init; }
    public DateTimeOffset? ArrivalWindowEnd { get; init; }
    public DateTimeOffset? DepartureWindowStart { get; init; }
    public DateTimeOffset? DepartureWindowEnd { get; init; }
    public string? Notes { get; init; }
}
