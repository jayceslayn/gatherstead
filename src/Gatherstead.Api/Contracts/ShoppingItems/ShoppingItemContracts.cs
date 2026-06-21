using System.ComponentModel.DataAnnotations;
using Gatherstead.Api.Contracts.Attributes;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Contracts.ShoppingItems;

public record ShoppingItemDto(
    Guid Id,
    Guid TenantId,
    ShoppingItemOrigin Origin,
    Guid? PropertyId,
    Guid? EventId,
    Guid? MealPlanId,
    string Name,
    decimal? QuantityNeeded,
    string? Unit,
    decimal? QuantityProvided,
    ShoppingItemStatus Status,
    Guid? ClaimedByMemberId,
    DateOnly? NeededByDate,
    string? Category,
    string? Notes,
    IReadOnlyList<AttributeDto> Attributes,
    AuditInfo? Audit);

public class ShoppingItemResponse : BaseEntityResponse<ShoppingItemDto> { }

/// <summary>
/// Creates a shopping item. Exactly one of <see cref="PropertyId"/>, <see cref="EventId"/>, or
/// <see cref="MealPlanId"/> must be supplied — it determines the item's origin scope. For meal
/// items the event and needed-by date are derived from the meal plan.
/// </summary>
public class CreateShoppingItemRequest
{
    public Guid? PropertyId { get; init; }
    public Guid? EventId { get; init; }
    public Guid? MealPlanId { get; init; }

    [Required]
    [StringLength(200)]
    public string Name { get; init; } = string.Empty;

    public decimal? QuantityNeeded { get; init; }

    [StringLength(50)]
    public string? Unit { get; init; }

    public DateOnly? NeededByDate { get; init; }

    [StringLength(50)]
    public string? Category { get; init; }

    [StringLength(500)]
    public string? Notes { get; init; }

    public IReadOnlyList<AttributeWriteEntry>? Attributes { get; init; }
}

/// <summary>Updates an item's structural fields (the menu/list line). Scope is immutable.</summary>
public class UpdateShoppingItemRequest
{
    [Required]
    [StringLength(200)]
    public string Name { get; init; } = string.Empty;

    public decimal? QuantityNeeded { get; init; }

    [StringLength(50)]
    public string? Unit { get; init; }

    public DateOnly? NeededByDate { get; init; }

    [StringLength(50)]
    public string? Category { get; init; }

    [StringLength(500)]
    public string? Notes { get; init; }

    public IReadOnlyList<AttributeWriteEntry>? Attributes { get; init; }
}

/// <summary>
/// Updates an item's fulfillment state (open to any tenant member). Re-flagging a needed item is
/// just setting <see cref="Status"/> back to Needed / lowering <see cref="QuantityProvided"/>.
/// </summary>
public class UpdateFulfillmentRequest
{
    [Required]
    public ShoppingItemStatus Status { get; init; }

    public decimal? QuantityProvided { get; init; }

    public Guid? ClaimedByMemberId { get; init; }
}
