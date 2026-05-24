using System.ComponentModel.DataAnnotations;
using Gatherstead.Api.Contracts.Attributes;
using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Contracts.EquipmentAttributes;

public class UpdateEquipmentAttributeRequest : IAttributeWriteRequest
{
    private string _key = string.Empty;
    private string _value = string.Empty;

    [Required]
    [StringLength(50)]
    public string Key
    {
        get => _key;
        init => _key = (value ?? string.Empty).Trim();
    }

    [Required]
    [StringLength(255)]
    public string Value
    {
        get => _value;
        init => _value = (value ?? string.Empty).Trim();
    }

    [Range(0, 4)]
    public byte TenantMinRole { get; init; } = (byte)TenantRole.Member;
}
