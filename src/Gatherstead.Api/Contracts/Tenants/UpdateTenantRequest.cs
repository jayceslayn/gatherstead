using System.ComponentModel.DataAnnotations;
using Gatherstead.Api.Contracts.Attributes;

namespace Gatherstead.Api.Contracts.Tenants;

public class UpdateTenantRequest
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
