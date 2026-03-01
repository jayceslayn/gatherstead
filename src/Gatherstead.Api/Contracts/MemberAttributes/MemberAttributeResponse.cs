using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Contracts.MemberAttributes;

public class MemberAttributeResponse : BaseEntityResponse<MemberAttributeDto>
{
}

public record MemberAttributeDto(
    Guid Id,
    Guid TenantId,
    Guid HouseholdMemberId,
    string Key,
    string Value,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool IsDeleted,
    DateTimeOffset? DeletedAt,
    Guid? DeletedByUserId);
