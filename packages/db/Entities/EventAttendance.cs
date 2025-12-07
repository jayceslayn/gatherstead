using System;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Db.Entities;

[Index(nameof(TenantId), nameof(EventId))]
[Index(nameof(TenantId), nameof(HouseholdMemberId))]
[Index(nameof(TenantId), nameof(EventId), nameof(HouseholdMemberId), nameof(Day), IsUnique = true)]
public class EventAttendance : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    [ForeignKey(nameof(TenantId))]
    public Tenant? Tenant { get; set; }

    public Guid EventId { get; set; }

    [ForeignKey(nameof(EventId))]
    public Event? Event { get; set; }

    public Guid HouseholdMemberId { get; set; }

    [ForeignKey(nameof(HouseholdMemberId))]
    public HouseholdMember? HouseholdMember { get; set; }

    public DateOnly Day { get; set; }
    public AttendanceStatus Status { get; set; }
    public DateTimeOffset? ArrivalWindowStart { get; set; }
    public DateTimeOffset? ArrivalWindowEnd { get; set; }
    public DateTimeOffset? DepartureWindowStart { get; set; }
    public DateTimeOffset? DepartureWindowEnd { get; set; }
    public string? Notes { get; set; }
}
