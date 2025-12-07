using System;

namespace Gatherstead.Db.Entities;

public class DietaryProfile : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid HouseholdMemberId { get; set; }
    public HouseholdMember? HouseholdMember { get; set; }
    public string PreferredDiet { get; set; } = string.Empty;
    public string[] Allergies { get; set; } = Array.Empty<string>();
    public string[] Restrictions { get; set; } = Array.Empty<string>();
    public string? Notes { get; set; }
}
