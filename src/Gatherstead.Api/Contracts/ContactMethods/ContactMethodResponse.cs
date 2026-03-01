using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Contracts.ContactMethods;

public class ContactMethodResponse : BaseEntityResponse<ContactMethodDto>
{
}

public record ContactMethodDto(
    Guid Id,
    Guid TenantId,
    Guid HouseholdMemberId,
    ContactMethodType Type,
    string Value,
    bool IsPrimary,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool IsDeleted,
    DateTimeOffset? DeletedAt,
    Guid? DeletedByUserId);
