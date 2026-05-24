using Gatherstead.Api.Contracts.Attributes;
using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Contracts.HouseholdAttributes;

public class HouseholdAttributeResponse : BaseEntityResponse<HouseholdAttributeDto>
{
}

public record HouseholdAttributeDto(
    Guid Id,
    Guid TenantId,
    Guid HouseholdId,
    string Key,
    string Value,
    byte TenantMinRole,
    byte? HouseholdMinRole,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool IsDeleted,
    DateTimeOffset? DeletedAt,
    Guid? DeletedByUserId) : IAttributeDto;
