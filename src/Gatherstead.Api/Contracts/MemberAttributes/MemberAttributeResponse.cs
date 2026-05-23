using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Contracts.MemberAttributes;

public class HouseholdMemberAttributeResponse : BaseEntityResponse<HouseholdMemberAttributeDto>
{
}

public record HouseholdMemberAttributeDto(
    Guid Id,
    Guid TenantId,
    Guid HouseholdMemberId,
    string Key,
    string Value,
    byte TenantMinRole,
    byte? HouseholdMinRole,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool IsDeleted,
    DateTimeOffset? DeletedAt,
    Guid? DeletedByUserId);
