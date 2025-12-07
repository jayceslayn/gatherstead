using System;

namespace Gatherstead.Db.Entities;

public class Address
{
    public Guid Id { get; set; }
    public Guid HouseholdMemberId { get; set; }
    public HouseholdMember? HouseholdMember { get; set; }
    public string Line1 { get; set; } = string.Empty;
    public string? Line2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }

    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public Guid? DeletedByUserId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
