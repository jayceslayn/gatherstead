using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Data.Entities;

[Index(nameof(TenantId), nameof(AccommodationId))]
[Index(nameof(TenantId), nameof(AccommodationId), nameof(Key), IsUnique = true)]
public class AccommodationAttribute : AuditableEntity, IParentScopedAttribute
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    [ForeignKey(nameof(TenantId))]
    public Tenant? Tenant { get; set; }

    public Guid AccommodationId { get; set; }

    [ForeignKey(nameof(AccommodationId))]
    public Accommodation? Accommodation { get; set; }

    [Required]
    [MaxLength(50)]
    public string Key { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string Value { get; set; } = string.Empty;

    public byte TenantMinRole { get; set; } = (byte)TenantRole.Member;
}
