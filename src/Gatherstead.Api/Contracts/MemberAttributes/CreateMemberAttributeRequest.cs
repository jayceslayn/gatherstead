using System.ComponentModel.DataAnnotations;

namespace Gatherstead.Api.Contracts.MemberAttributes;

public class CreateMemberAttributeRequest
{
    [Required]
    [StringLength(100)]
    public string Key { get; init; } = string.Empty;

    [Required]
    [StringLength(500)]
    public string Value { get; init; } = string.Empty;
}
