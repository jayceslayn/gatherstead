using System.ComponentModel.DataAnnotations;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Contracts.ChoreTemplates;

public record ChoreTemplateDto(
    Guid Id,
    Guid TenantId,
    Guid EventId,
    string Name,
    ChoreTimeSlotFlags TimeSlots,
    int? MinimumAssignees,
    string? Notes,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool IsDeleted,
    DateTimeOffset? DeletedAt,
    Guid? DeletedByUserId);

public class ChoreTemplateResponse : BaseEntityResponse<ChoreTemplateDto> { }

public class CreateChoreTemplateRequest
{
    [Required]
    [StringLength(200)]
    public string Name { get; init; } = string.Empty;

    [Required]
    public ChoreTimeSlotFlags TimeSlots { get; init; }

    public int? MinimumAssignees { get; init; }
    public string? Notes { get; init; }
}

public class UpdateChoreTemplateRequest
{
    [Required]
    [StringLength(200)]
    public string Name { get; init; } = string.Empty;

    [Required]
    public ChoreTimeSlotFlags TimeSlots { get; init; }

    public int? MinimumAssignees { get; init; }
    public string? Notes { get; init; }
}
