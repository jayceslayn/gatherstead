using System.ComponentModel.DataAnnotations;
using Gatherstead.Api.Contracts.Attributes;
using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Contracts.Equipment;

public record EquipmentDto(
    Guid Id,
    Guid TenantId,
    Guid? PropertyId,
    string Name,
    string? Notes,
    IReadOnlyList<AttributeDto> Attributes,
    AuditInfo? Audit);

public class EquipmentResponse : BaseEntityResponse<EquipmentDto> { }

public class CreateEquipmentRequest
{
    public Guid? PropertyId { get; init; }

    [Required]
    [StringLength(200)]
    public string Name { get; init; } = string.Empty;

    [StringLength(500)]
    public string? Notes { get; init; }

    public IReadOnlyList<AttributeWriteEntry>? Attributes { get; init; }
}

public class UpdateEquipmentRequest
{
    public Guid? PropertyId { get; init; }

    [Required]
    [StringLength(200)]
    public string Name { get; init; } = string.Empty;

    [StringLength(500)]
    public string? Notes { get; init; }

    public IReadOnlyList<AttributeWriteEntry>? Attributes { get; init; }
}
