using System.ComponentModel.DataAnnotations;
using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Contracts.ContactMethods;

public class UpdateContactMethodRequest
{
    private string _value = string.Empty;

    [Required]
    public ContactMethodType Type { get; init; }

    [Required]
    [StringLength(256)]
    public string Value
    {
        get => _value;
        init => _value = (value ?? string.Empty).Trim();
    }

    public bool IsPrimary { get; init; }
}
