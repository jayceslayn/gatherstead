using System.ComponentModel.DataAnnotations;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Contracts.ContactMethods;

public record ContactMethodDto(
    Guid Id,
    Guid TenantId,
    Guid HouseholdMemberId,
    ContactMethodType Type,
    string Value,
    bool IsPrimary,
    AuditInfo? Audit);

public class ContactMethodResponse : BaseEntityResponse<ContactMethodDto> { }

public class CreateContactMethodRequest
{
    [Required]
    public ContactMethodType Type { get; init; }

    [Required]
    [StringLength(256)]
    public string Value { get; init; } = string.Empty;

    public bool IsPrimary { get; init; }
}

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
