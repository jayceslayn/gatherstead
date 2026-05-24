using System.ComponentModel.DataAnnotations;
using Gatherstead.Api.Contracts.Attributes;
using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Contracts.EventAttributes;

public class CreateEventAttributeRequest : IAttributeWriteRequest
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
