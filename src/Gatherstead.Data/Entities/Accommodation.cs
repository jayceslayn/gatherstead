using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Data.Entities;

[Index(nameof(TenantId), nameof(PropertyId))]
[Index(nameof(TenantId), nameof(PropertyId), nameof(Name), IsUnique = true)]
public class Accommodation : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    [ForeignKey(nameof(TenantId))]
    public Tenant? Tenant { get; set; }

    public Guid PropertyId { get; set; }

    [ForeignKey(nameof(PropertyId))]
    public Property? Property { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public AccommodationType Type { get; set; }
    public int? CapacityAdults { get; set; }
    public int? CapacityChildren { get; set; }
    public string? Notes { get; set; }

    public ICollection<AccommodationIntent> AccommodationIntents { get; set; } = new List<AccommodationIntent>();
}
