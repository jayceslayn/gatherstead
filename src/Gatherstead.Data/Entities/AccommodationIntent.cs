using System;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Data.Entities;

[Index(nameof(TenantId), nameof(HouseholdMemberId), nameof(Night))]
[Index(nameof(TenantId), nameof(AccommodationId), nameof(Night), nameof(HouseholdMemberId), IsUnique = true)]
public class AccommodationIntent : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    [ForeignKey(nameof(TenantId))]
    public Tenant? Tenant { get; set; }

    public Guid HouseholdMemberId { get; set; }

    [ForeignKey(nameof(HouseholdMemberId))]
    public HouseholdMember? HouseholdMember { get; set; }

    public Guid AccommodationId { get; set; }

    [ForeignKey(nameof(AccommodationId))]
    public Accommodation? Accommodation { get; set; }

    public DateOnly Night { get; set; }
    public AccommodationIntentStatus Status { get; set; }
    public string? Notes { get; set; }

    public AccommodationIntentDecision Decision { get; set; }
    public int? PartySize { get; set; }
    public int? Priority { get; set; }
}
