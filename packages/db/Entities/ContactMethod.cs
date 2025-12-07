using System;

namespace Gatherstead.Db.Entities;

public class ContactMethod
{
    public Guid Id { get; set; }
    public Guid HouseholdMemberId { get; set; }
    public HouseholdMember? HouseholdMember { get; set; }
    public ContactMethodType Type { get; set; }
    public string Value { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }

    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public Guid? DeletedByUserId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
