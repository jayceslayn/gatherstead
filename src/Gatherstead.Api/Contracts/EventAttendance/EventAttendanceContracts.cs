using System.ComponentModel.DataAnnotations;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Contracts.EventAttendance;

public record EventAttendanceDto(
    [property: Required] Guid Id,
    [property: Required] Guid TenantId,
    [property: Required] Guid EventId,
    [property: Required] Guid HouseholdMemberId,
    [property: Required] DateOnly Day,
    [property: Required] AttendanceStatus Status,
    string? Notes,
    AuditInfo? Audit);

public class EventAttendanceResponse : BaseEntityResponse<EventAttendanceDto> { }

public class UpsertEventAttendanceRequest
{
    [Required]
    public Guid HouseholdMemberId { get; init; }

    [Required]
    public DateOnly Day { get; init; }

    [Required]
    public AttendanceStatus Status { get; init; }

    public string? Notes { get; init; }
}

public class BulkUpsertEventAttendanceRequest
{
    [Required]
    public IReadOnlyList<UpsertEventAttendanceRequest> Items { get; init; } = Array.Empty<UpsertEventAttendanceRequest>();
}
