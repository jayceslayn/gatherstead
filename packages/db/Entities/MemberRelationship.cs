using System;

namespace Gatherstead.Db.Entities;

public class MemberRelationship
{
    public Guid Id { get; set; }
    public Guid HouseholdMemberId { get; set; }
    public HouseholdMember? HouseholdMember { get; set; }
    public Guid RelatedMemberId { get; set; }
    public HouseholdMember? RelatedMember { get; set; }
    public RelationshipType RelationshipType { get; set; }
    public string? Notes { get; set; }

    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public Guid? DeletedByUserId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
