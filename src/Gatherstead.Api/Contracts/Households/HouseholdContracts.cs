using System.ComponentModel.DataAnnotations;
using Gatherstead.Api.Contracts.Attributes;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Contracts.Households;

// CallerRole is the requesting user's HouseholdRole for this household (null when they hold no
// HouseholdUser row — e.g. an app admin or a tenant Manager acting via their tenant role). The UI
// combines it with the caller's tenant role to decide whether to surface member-management controls;
// the API still enforces authorization on both axes regardless.
public record HouseholdDto(
    Guid Id,
    Guid TenantId,
    string Name,
    string? Notes,
    IReadOnlyList<AttributeDto> Attributes,
    HouseholdRole? CallerRole,
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
