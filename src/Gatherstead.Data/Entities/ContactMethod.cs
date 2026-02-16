using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Data.Entities;

[Index(nameof(TenantId), nameof(HouseholdMemberId), Name = "IX_Contact_PrimaryPerMember", IsUnique = true)]
public class ContactMethod : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    [ForeignKey(nameof(TenantId))]
    public Tenant? Tenant { get; set; }
    public Guid HouseholdMemberId { get; set; }
    [ForeignKey(nameof(HouseholdMemberId))]
    public HouseholdMember? HouseholdMember { get; set; }
    public ContactMethodType Type { get; set; }
    [Required]
    [MaxLength(256)]
    public string Value { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
}
