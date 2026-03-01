using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Contracts.MemberRelationships;

public class MemberRelationshipResponse : BaseEntityResponse<MemberRelationshipDto>
{
}

public record MemberRelationshipDto(
    Guid Id,
    Guid TenantId,
    Guid HouseholdMemberId,
    Guid RelatedMemberId,
    RelationshipType RelationshipType,
    string? Notes,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool IsDeleted,
    DateTimeOffset? DeletedAt,
    Guid? DeletedByUserId);
