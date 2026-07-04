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

    /// <summary>Footprint width in metres. Feet and area are derived for display.</summary>
    [Column(TypeName = "decimal(6,2)")]
    public decimal? WidthMeters { get; set; }

    /// <summary>Footprint depth in metres.</summary>
    [Column(TypeName = "decimal(6,2)")]
    public decimal? DepthMeters { get; set; }

    /// <summary>Optional area override (m²) for irregular spaces where width × depth does not apply.</summary>
    [Column(TypeName = "decimal(6,2)")]
    public decimal? AreaSqMeters { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public ICollection<AccommodationBed> Beds { get; set; } = new List<AccommodationBed>();
    public ICollection<AccommodationIntent> AccommodationIntents { get; set; } = new List<AccommodationIntent>();
    public ICollection<AccommodationAttribute> Attributes { get; set; } = new List<AccommodationAttribute>();
}
