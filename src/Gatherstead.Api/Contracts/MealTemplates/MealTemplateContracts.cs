using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Gatherstead.Api.Contracts.Attributes;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Contracts.MealTemplates;

public record MealTemplateDto(
    Guid Id,
    Guid TenantId,
    Guid EventId,
    string Name,
    [property: JsonConverter(typeof(JsonNumberEnumConverter<MealTypeFlags>))] MealTypeFlags MealTypes,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? Notes,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool IsDeleted,
    DateTimeOffset? DeletedAt,
    Guid? DeletedByUserId,
    IReadOnlyList<AttributeDto> Attributes);

public class MealTemplateResponse : BaseEntityResponse<MealTemplateDto> { }

public class CreateMealTemplateRequest
{
    [Required]
    [StringLength(200)]
    public string Name { get; init; } = string.Empty;

    [Required]
    [JsonConverter(typeof(JsonNumberEnumConverter<MealTypeFlags>))]
    public MealTypeFlags MealTypes { get; init; }

    public DateOnly? StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public string? Notes { get; init; }
    public IReadOnlyList<AttributeWriteEntry>? Attributes { get; init; }

    /// <summary>
    /// When true, a matching <see cref="TaskTemplate"/> is created alongside the meal so the
    /// meal can also be organized/assigned as a task. Meal types map to task time slots
    /// (Breakfast→Morning, Lunch→Midday, Dinner→Evening) and the same date sub-range is used.
    /// </summary>
    public bool CreateMatchingTaskTemplate { get; init; }
}

public class UpdateMealTemplateRequest
{
    [Required]
    [StringLength(200)]
    public string Name { get; init; } = string.Empty;

    [Required]
    [JsonConverter(typeof(JsonNumberEnumConverter<MealTypeFlags>))]
    public MealTypeFlags MealTypes { get; init; }

    public DateOnly? StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public string? Notes { get; init; }
    public IReadOnlyList<AttributeWriteEntry>? Attributes { get; init; }
}
