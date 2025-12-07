using System;

namespace Gatherstead.Db.Entities;

public class EventAttendance : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid EventId { get; set; }
    public Event? Event { get; set; }
    public Guid HouseholdMemberId { get; set; }
    public HouseholdMember? HouseholdMember { get; set; }
    public DateOnly Day { get; set; }
    public AttendanceStatus Status { get; set; }
    public DateTimeOffset? ArrivalWindowStart { get; set; }
    public DateTimeOffset? ArrivalWindowEnd { get; set; }
    public DateTimeOffset? DepartureWindowStart { get; set; }
    public DateTimeOffset? DepartureWindowEnd { get; set; }
    public string? Notes { get; set; }
}
