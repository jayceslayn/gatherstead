using System.ComponentModel.DataAnnotations;
using Gatherstead.Api.Contracts.Attributes;
using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Contracts.Households;

public record HouseholdDto(
    Guid Id,
    Guid TenantId,
    string Name,
    string? Notes,
    IReadOnlyList<AttributeDto> Attributes,
    AuditInfo? Audit);

public class HouseholdResponse : BaseEntityResponse<HouseholdDto> { }

public class CreateHouseholdRequest
{
    [Required]
    [StringLength(200)]
    public string Name { get; init; } = string.Empty;

    [StringLength(500)]
    public string? Notes { get; init; } = null;

    public IReadOnlyList<AttributeWriteEntry>? Attributes { get; init; }
}

public class UpdateHouseholdRequest
{
    private string _name = string.Empty;

    [Required]
    [StringLength(200)]
    public string Name
    {
        get => _name;
        init => _name = (value ?? string.Empty).Trim();
    }

    [StringLength(500)]
    public string? Notes { get; init; } = null;

    public IReadOnlyList<AttributeWriteEntry>? Attributes { get; init; }
}
