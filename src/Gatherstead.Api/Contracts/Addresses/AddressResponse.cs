using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Contracts.Addresses;

public class AddressResponse : BaseEntityResponse<AddressDto>
{
}

public record AddressDto(
    Guid Id,
    Guid TenantId,
    Guid HouseholdMemberId,
    string Line1,
    string? Line2,
    string City,
    string State,
    string PostalCode,
    string Country,
    bool IsPrimary,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool IsDeleted,
    DateTimeOffset? DeletedAt,
    Guid? DeletedByUserId);
