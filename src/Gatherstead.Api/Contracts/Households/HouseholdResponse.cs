using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Contracts.Households;

public class HouseholdResponse : BaseEntityResponse<HouseholdDto>
{
}

public record HouseholdDto(
    Guid Id,
    Guid TenantId,
    string Name,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool IsDeleted,
    DateTimeOffset? DeletedAt,
    Guid? DeletedByUserId);
