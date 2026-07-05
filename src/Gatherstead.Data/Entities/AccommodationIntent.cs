using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Data.Entities;

[Index(nameof(TenantId), nameof(HouseholdMemberId))]
[Index(nameof(TenantId), nameof(AccommodationId), nameof(StartNight), nameof(EndNight))]
// Blocks true duplicate stays (identical member + accommodation + span) while still allowing a
// member to hold multiple, even overlapping, non-identical spans. Backed by the upsert-with-revive
// in AccommodationIntentService.CreateAsync so a re-request revives rather than colliding.
[Index(nameof(TenantId), nameof(AccommodationId), nameof(HouseholdMemberId), nameof(StartNight), nameof(EndNight),
    IsUnique = true, Name = "IX_AccommodationIntent_UniqueStay")]
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

    /// <summary>First night of the stay (inclusive).</summary>
    public DateOnly StartNight { get; set; }

    /// <summary>Last night of the stay (inclusive). A single-night stay has StartNight == EndNight.</summary>
    public DateOnly EndNight { get; set; }

    public AccommodationIntentStatus Status { get; set; }
    [MaxLength(500)]
    public string? Notes { get; set; }

    public int? PartyAdults { get; set; }
    public int? PartyChildren { get; set; }
}
