using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Contracts.TenantAttributes;

public class TenantAttributeResponse : BaseEntityResponse<TenantAttributeDto>
{
}

public record TenantAttributeDto(
    Guid Id,
    Guid TenantId,
    string Key,
    string Value,
    byte TenantMinRole,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool IsDeleted,
    DateTimeOffset? DeletedAt,
    Guid? DeletedByUserId);
