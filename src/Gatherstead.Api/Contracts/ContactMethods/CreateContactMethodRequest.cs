using System.ComponentModel.DataAnnotations;
using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Contracts.ContactMethods;

public class CreateContactMethodRequest
{
    [Required]
    public ContactMethodType Type { get; init; }

    [Required]
    [StringLength(256)]
    public string Value { get; init; } = string.Empty;

    public bool IsPrimary { get; init; }
}
