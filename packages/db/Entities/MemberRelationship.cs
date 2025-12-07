using System;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Db.Entities;

[Index(nameof(TenantId), nameof(HouseholdMemberId))]
[Index(nameof(TenantId), nameof(RelatedMemberId))]
[Index(nameof(TenantId), nameof(HouseholdMemberId), nameof(RelatedMemberId), nameof(RelationshipType), IsUnique = true)]
public class MemberRelationship : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    [ForeignKey(nameof(TenantId))]
    public Tenant? Tenant { get; set; }

    public Guid HouseholdMemberId { get; set; }

    [ForeignKey(nameof(HouseholdMemberId))]
    public HouseholdMember? HouseholdMember { get; set; }

    public Guid RelatedMemberId { get; set; }

    [ForeignKey(nameof(RelatedMemberId))]
    public HouseholdMember? RelatedMember { get; set; }

    public RelationshipType RelationshipType { get; set; }
    public string? Notes { get; set; }
}
