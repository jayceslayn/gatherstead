using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Data.Entities;

[Index(nameof(TenantId), nameof(EventId))]
[Index(nameof(TenantId), nameof(PropertyId))]
[Index(nameof(TenantId), nameof(MealPlanId))]
public class ShoppingItem : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    [ForeignKey(nameof(TenantId))]
    public Tenant? Tenant { get; set; }

    /// <summary>Which scope this item originated from. Exactly one matching scope FK is set.</summary>
    public ShoppingItemOrigin Origin { get; set; }

    /// <summary>Set when <see cref="Origin"/> is Property.</summary>
    public Guid? PropertyId { get; set; }

    [ForeignKey(nameof(PropertyId))]
    public Property? Property { get; set; }

    /// <summary>
    /// Set for Event and Meal origins. For meal items it is denormalized from the meal's event
    /// (an immutable parent) so the merged event view can filter on a single column.
    /// </summary>
    public Guid? EventId { get; set; }

    [ForeignKey(nameof(EventId))]
    public Event? Event { get; set; }

    /// <summary>Set when <see cref="Origin"/> is Meal.</summary>
    public Guid? MealPlanId { get; set; }

    [ForeignKey(nameof(MealPlanId))]
    public MealPlan? MealPlan { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Free-text quantity; no unit conversion is performed.</summary>
    public decimal? QuantityNeeded { get; set; }

    [MaxLength(50)]
    public string? Unit { get; set; }

    /// <summary>Auto-derived from <see cref="MealPlan"/>.Day for meal items; manual otherwise.</summary>
    public DateOnly? NeededByDate { get; set; }

    [MaxLength(50)]
    public string? Category { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public ICollection<ShoppingItemAttribute> Attributes { get; set; } = new List<ShoppingItemAttribute>();

    /// <summary>Per-member contributions. The item's status and provided total are derived from these.</summary>
    public ICollection<ShoppingItemIntent> Intents { get; set; } = new List<ShoppingItemIntent>();
}
