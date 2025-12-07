using System;

namespace Gatherstead.Db.Entities;

public class DietaryProfile
{
    public Guid Id { get; set; }
    public Guid HouseholdMemberId { get; set; }
    public HouseholdMember? HouseholdMember { get; set; }
    public string PreferredDiet { get; set; } = string.Empty;
    public string[] Allergies { get; set; } = Array.Empty<string>();
    public string[] Restrictions { get; set; } = Array.Empty<string>();
    public string? Notes { get; set; }

    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public Guid? DeletedByUserId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
