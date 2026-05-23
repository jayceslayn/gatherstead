using System.ComponentModel.DataAnnotations;
using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Contracts.MemberAttributes;

public class CreateHouseholdMemberAttributeRequest
{
    [Required]
    [StringLength(50)]
    public string Key { get; init; } = string.Empty;

    [Required]
    [StringLength(255)]
    public string Value { get; init; } = string.Empty;

    [Range(0, 4)]
    public byte TenantMinRole { get; init; } = (byte)TenantRole.Member;

    [Range(0, 1)]
    public byte? HouseholdMinRole { get; init; }
}
