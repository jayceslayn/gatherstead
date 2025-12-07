using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Db.Entities;

[Index(nameof(TenantId), nameof(HouseholdMemberId), IsUnique = true)]
public class DietaryProfile : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    [ForeignKey(nameof(TenantId))]
    public Tenant? Tenant { get; set; }

    public Guid HouseholdMemberId { get; set; }

    [ForeignKey(nameof(HouseholdMemberId))]
    public HouseholdMember? HouseholdMember { get; set; }

    [MaxLength(200)]
    public string PreferredDiet { get; set; } = string.Empty;

    public string[] Allergies { get; set; } = Array.Empty<string>();
    public string[] Restrictions { get; set; } = Array.Empty<string>();
    public string? Notes { get; set; }
}
