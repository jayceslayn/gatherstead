using System.ComponentModel.DataAnnotations;

namespace Gatherstead.Api.Contracts.Equipment;

public class CreateEquipmentRequest
{
    public Guid? PropertyId { get; init; }

    [Required]
    [StringLength(200)]
    public string Name { get; init; } = string.Empty;

    [StringLength(500)]
    public string? Notes { get; init; }
}
