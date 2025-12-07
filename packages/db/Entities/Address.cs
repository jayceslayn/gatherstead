using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Db.Entities;

[Index(nameof(TenantId), nameof(HouseholdMemberId), Name = "IX_Address_PrimaryPerMember", IsUnique = true)]
public class Address : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    [ForeignKey(nameof(TenantId))]
    public Tenant? Tenant { get; set; }
    public Guid HouseholdMemberId { get; set; }
    [ForeignKey(nameof(HouseholdMemberId))]
    public HouseholdMember? HouseholdMember { get; set; }
    [Required]
    [MaxLength(200)]
    public string Line1 { get; set; } = string.Empty;
    [MaxLength(200)]
    public string? Line2 { get; set; }
    [Required]
    [MaxLength(100)]
    public string City { get; set; } = string.Empty;
    [Required]
    [MaxLength(100)]
    public string State { get; set; } = string.Empty;
    [Required]
    [MaxLength(20)]
    public string PostalCode { get; set; } = string.Empty;
    [Required]
    [MaxLength(100)]
    public string Country { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
}
