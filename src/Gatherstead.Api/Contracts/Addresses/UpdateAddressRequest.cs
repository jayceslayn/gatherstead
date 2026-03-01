using System.ComponentModel.DataAnnotations;

namespace Gatherstead.Api.Contracts.Addresses;

public class UpdateAddressRequest
{
    private string _line1 = string.Empty;
    private string _city = string.Empty;
    private string _state = string.Empty;
    private string _postalCode = string.Empty;
    private string _country = string.Empty;

    [Required]
    [StringLength(200)]
    public string Line1
    {
        get => _line1;
        init => _line1 = (value ?? string.Empty).Trim();
    }

    [StringLength(200)]
    public string? Line2 { get; init; }

    [Required]
    [StringLength(100)]
    public string City
    {
        get => _city;
        init => _city = (value ?? string.Empty).Trim();
    }

    [Required]
    [StringLength(100)]
    public string State
    {
        get => _state;
        init => _state = (value ?? string.Empty).Trim();
    }

    [Required]
    [StringLength(20)]
    public string PostalCode
    {
        get => _postalCode;
        init => _postalCode = (value ?? string.Empty).Trim();
    }

    [Required]
    [StringLength(100)]
    public string Country
    {
        get => _country;
        init => _country = (value ?? string.Empty).Trim();
    }

    public bool IsPrimary { get; init; }
}
