using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Data.Entities;

[Index(nameof(TenantId), nameof(HouseholdId), nameof(Name))]
public class HouseholdMember : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    [ForeignKey(nameof(TenantId))]
    public Tenant? Tenant { get; set; }
    public Guid HouseholdId { get; set; }
    [ForeignKey(nameof(HouseholdId))]
    public Household? Household { get; set; }

    public Guid? UserId { get; set; }
    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    public HouseholdRole HouseholdRole { get; set; } = HouseholdRole.Member;

    public bool IsAdult { get; set; }
    [MaxLength(64)]
    public string? AgeBand { get; set; }

    // Encrypted fields
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    public DateOnly? BirthDate { get; set; }
    public string? DietaryNotes { get; set; }

    public string[] DietaryTags { get; set; } = Array.Empty<string>();

    public ICollection<MemberRelationship> Relationships { get; set; } = new List<MemberRelationship>();
    public ICollection<ContactMethod> ContactMethods { get; set; } = new List<ContactMethod>();
    public ICollection<Address> Addresses { get; set; } = new List<Address>();
    public ICollection<MemberAttribute> Attributes { get; set; } = new List<MemberAttribute>();
    public DietaryProfile? DietaryProfile { get; set; }
}
