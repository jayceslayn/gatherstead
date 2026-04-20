using System.ComponentModel.DataAnnotations;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Contracts.MealTemplates;

public record MealTemplateDto(
    Guid Id,
    Guid TenantId,
    Guid EventId,
    string Name,
    MealTypeFlags MealTypes,
    string? Notes,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool IsDeleted,
    DateTimeOffset? DeletedAt,
    Guid? DeletedByUserId);

public class MealTemplateResponse : BaseEntityResponse<MealTemplateDto> { }

public class CreateMealTemplateRequest
{
    [Required]
    [StringLength(200)]
    public string Name { get; init; } = string.Empty;

    [Required]
    public MealTypeFlags MealTypes { get; init; }

    public string? Notes { get; init; }
}

public class UpdateMealTemplateRequest
{
    [Required]
    [StringLength(200)]
    public string Name { get; init; } = string.Empty;

    [Required]
    public MealTypeFlags MealTypes { get; init; }

    public string? Notes { get; init; }
}
