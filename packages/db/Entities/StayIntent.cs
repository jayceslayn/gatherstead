using System;
using Gatherstead.Db.Encryption;

namespace Gatherstead.Db.Entities;

public class StayIntent : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid HouseholdMemberId { get; set; }
    public HouseholdMember? HouseholdMember { get; set; }
    public Guid ResourceId { get; set; }
    public Resource? Resource { get; set; }
    public DateOnly Night { get; set; }
    public StayIntentStatus Status { get; set; }
    public string? Notes { get; set; }

    public StayIntentDecision Decision { get; set; }
    public int? PartySize { get; set; }
    public int? Priority { get; set; }
}
