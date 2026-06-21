using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Data.Entities;

/// <summary>
/// One member's contribution toward a <see cref="ShoppingItem"/>. Multiple members can each cover
/// part of an item; the item's status and provided total are derived from its intents. At most one
/// intent per member per item (enforced by the unique index).
/// </summary>
[Index(nameof(TenantId), nameof(HouseholdMemberId))]
[Index(nameof(TenantId), nameof(ShoppingItemId))]
[Index(nameof(TenantId), nameof(ShoppingItemId), nameof(HouseholdMemberId), IsUnique = true)]
public class ShoppingItemIntent : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    [ForeignKey(nameof(TenantId))]
    public Tenant? Tenant { get; set; }

    public Guid ShoppingItemId { get; set; }

    [ForeignKey(nameof(ShoppingItemId))]
    public ShoppingItem? ShoppingItem { get; set; }

    public Guid HouseholdMemberId { get; set; }

    [ForeignKey(nameof(HouseholdMemberId))]
    public HouseholdMember? HouseholdMember { get; set; }

    /// <summary>Amount this member is bringing. Null = the whole/unspecified quantity.</summary>
    public decimal? Quantity { get; set; }

    public ShoppingItemIntentStatus Status { get; set; } = ShoppingItemIntentStatus.Claimed;

    [MaxLength(500)]
    public string? Notes { get; set; }
}
