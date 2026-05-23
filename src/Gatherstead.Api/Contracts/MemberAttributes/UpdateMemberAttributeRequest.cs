using System.ComponentModel.DataAnnotations;
using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Contracts.MemberAttributes;

public class UpdateHouseholdMemberAttributeRequest
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

    [Range(0, 1)]
    public byte? HouseholdMinRole { get; init; }
}
