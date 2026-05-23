using System.ComponentModel.DataAnnotations;
using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Contracts.PropertyAttributes;

public class CreatePropertyAttributeRequest
{
    [Required]
    [StringLength(50)]
    public string Key { get; init; } = string.Empty;

    [Required]
    [StringLength(255)]
    public string Value { get; init; } = string.Empty;

    [Range(0, 4)]
    public byte TenantMinRole { get; init; } = (byte)TenantRole.Member;
}
