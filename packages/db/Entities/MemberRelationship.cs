using System;

namespace Gatherstead.Db.Entities;

public class MemberRelationship : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid HouseholdMemberId { get; set; }
    public HouseholdMember? HouseholdMember { get; set; }
    public Guid RelatedMemberId { get; set; }
    public HouseholdMember? RelatedMember { get; set; }
    public RelationshipType RelationshipType { get; set; }
    public string? Notes { get; set; }
}
