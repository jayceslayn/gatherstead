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
    DateOnly? NeededByDate,
    string? Category,
    string? Notes,
    IReadOnlyList<AttributeDto> Attributes,
    IReadOnlyList<ShoppingItemIntentDto> Intents,
    AuditInfo? Audit);

/// <summary>One member's contribution toward an item. <see cref="QuantityProvided"/> on the parent
/// is the sum of these, and the parent's status is derived from them.</summary>
public record ShoppingItemIntentDto(
    Guid Id,
    Guid HouseholdMemberId,
    decimal? Quantity,
    ShoppingItemIntentStatus Status,
    string? Notes);

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
/// Creates or updates a single member's contribution toward an item (open to any tenant member).
/// The parent item's provided total and status are derived from its intents — removing the intent
/// (DELETE) un-claims the member's share.
/// </summary>
public class UpsertShoppingItemIntentRequest
{
    /// <summary>Amount this member is bringing. Null = the whole/unspecified quantity.</summary>
    public decimal? Quantity { get; init; }

    [Required]
    public ShoppingItemIntentStatus Status { get; init; }

    [StringLength(500)]
    public string? Notes { get; init; }
}
