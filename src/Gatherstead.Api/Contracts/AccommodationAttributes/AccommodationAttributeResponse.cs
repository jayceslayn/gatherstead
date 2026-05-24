using Gatherstead.Api.Contracts.Attributes;
using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Contracts.AccommodationAttributes;

public class AccommodationAttributeResponse : BaseEntityResponse<AccommodationAttributeDto>
{
}

public record AccommodationAttributeDto(
    Guid Id,
    Guid TenantId,
    Guid AccommodationId,
    string Key,
    string Value,
    byte TenantMinRole,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool IsDeleted,
    DateTimeOffset? DeletedAt,
    Guid? DeletedByUserId) : IAttributeDto;
