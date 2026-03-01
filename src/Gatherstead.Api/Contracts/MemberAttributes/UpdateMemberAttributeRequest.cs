using System.ComponentModel.DataAnnotations;

namespace Gatherstead.Api.Contracts.MemberAttributes;

public class UpdateMemberAttributeRequest
{
    private string _key = string.Empty;
    private string _value = string.Empty;

    [Required]
    [StringLength(100)]
    public string Key
    {
        get => _key;
        init => _key = (value ?? string.Empty).Trim();
    }

    [Required]
    [StringLength(500)]
    public string Value
    {
        get => _value;
        init => _value = (value ?? string.Empty).Trim();
    }
}
