using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Contracts.PropertyAttributes;

public class PropertyAttributeResponse : BaseEntityResponse<PropertyAttributeDto>
{
}

public record PropertyAttributeDto(
    Guid Id,
    Guid TenantId,
    Guid PropertyId,
    string Key,
    string Value,
    byte TenantMinRole,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool IsDeleted,
    DateTimeOffset? DeletedAt,
    Guid? DeletedByUserId);
