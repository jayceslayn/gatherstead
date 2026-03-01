using System.ComponentModel.DataAnnotations;

namespace Gatherstead.Api.Contracts.Addresses;

public class CreateAddressRequest
{
    [Required]
    [StringLength(200)]
    public string Line1 { get; init; } = string.Empty;

    [StringLength(200)]
    public string? Line2 { get; init; }

    [Required]
    [StringLength(100)]
    public string City { get; init; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string State { get; init; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string PostalCode { get; init; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Country { get; init; } = string.Empty;

    public bool IsPrimary { get; init; }
}
