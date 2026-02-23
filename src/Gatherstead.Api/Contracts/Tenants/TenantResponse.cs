using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Contracts.Tenants;

public class TenantResponse : BaseEntityResponse<TenantDto>
{
}

public record TenantDto(
    Guid Id,
    string Name,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool IsDeleted,
    DateTimeOffset? DeletedAt,
    Guid? DeletedByUserId);
