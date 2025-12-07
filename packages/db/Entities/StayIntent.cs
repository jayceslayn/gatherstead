using System;
using System.ComponentModel.DataAnnotations.Schema;
using Gatherstead.Db.Encryption;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Db.Entities;

[Index(nameof(TenantId), nameof(HouseholdMemberId))]
[Index(nameof(TenantId), nameof(ResourceId))]
[Index(nameof(TenantId), nameof(ResourceId), nameof(Night), nameof(HouseholdMemberId), IsUnique = true)]
public class StayIntent : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    [ForeignKey(nameof(TenantId))]
    public Tenant? Tenant { get; set; }

    public Guid HouseholdMemberId { get; set; }

    [ForeignKey(nameof(HouseholdMemberId))]
    public HouseholdMember? HouseholdMember { get; set; }

    public Guid ResourceId { get; set; }

    [ForeignKey(nameof(ResourceId))]
    public Resource? Resource { get; set; }

    public DateOnly Night { get; set; }
    public StayIntentStatus Status { get; set; }
    public string? Notes { get; set; }

    public StayIntentDecision Decision { get; set; }
    public int? PartySize { get; set; }
    public int? Priority { get; set; }
}
