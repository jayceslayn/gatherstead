using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Data.Entities;

[Index(nameof(TenantId), nameof(HouseholdId))]
[Index(nameof(TenantId), nameof(HouseholdId), nameof(Key), IsUnique = true)]
public class HouseholdAttribute : AuditableEntity, IHouseholdScopedAttribute
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    [ForeignKey(nameof(TenantId))]
    public Tenant? Tenant { get; set; }

    public Guid HouseholdId { get; set; }

    [ForeignKey(nameof(HouseholdId))]
    public Household? Household { get; set; }

    [Required]
    [MaxLength(50)]
    public string Key { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string Value { get; set; } = string.Empty;

    public byte TenantMinRole { get; set; } = (byte)TenantRole.Member;
    public byte? HouseholdMinRole { get; set; }
}
