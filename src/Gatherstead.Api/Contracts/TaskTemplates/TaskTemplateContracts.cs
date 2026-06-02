using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Gatherstead.Api.Contracts.Attributes;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Contracts.TaskTemplates;

public record TaskTemplateDto(
    Guid Id,
    Guid TenantId,
    Guid EventId,
    string Name,
    [property: JsonConverter(typeof(JsonNumberEnumConverter<TaskTimeSlotFlags>))] TaskTimeSlotFlags TimeSlots,
    DateOnly? StartDate,
    DateOnly? EndDate,
    int? MinimumAssignees,
    string? Notes,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool IsDeleted,
    DateTimeOffset? DeletedAt,
    Guid? DeletedByUserId,
    IReadOnlyList<AttributeDto> Attributes);

public class TaskTemplateResponse : BaseEntityResponse<TaskTemplateDto> { }

public class CreateTaskTemplateRequest
{
    [Required]
    [StringLength(200)]
    public string Name { get; init; } = string.Empty;

    [Required]
    [JsonConverter(typeof(JsonNumberEnumConverter<TaskTimeSlotFlags>))]
    public TaskTimeSlotFlags TimeSlots { get; init; }

    public DateOnly? StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public int? MinimumAssignees { get; init; }
    public string? Notes { get; init; }
    public IReadOnlyList<AttributeWriteEntry>? Attributes { get; init; }
}

public class UpdateTaskTemplateRequest
{
    [Required]
    [StringLength(200)]
    public string Name { get; init; } = string.Empty;

    [Required]
    [JsonConverter(typeof(JsonNumberEnumConverter<TaskTimeSlotFlags>))]
    public TaskTimeSlotFlags TimeSlots { get; init; }

    public DateOnly? StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public int? MinimumAssignees { get; init; }
    public string? Notes { get; init; }
    public IReadOnlyList<AttributeWriteEntry>? Attributes { get; init; }
}
