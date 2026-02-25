using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Contracts.HouseholdMembers;

public class HouseholdMemberResponse : BaseEntityResponse<HouseholdMemberDto>
{
}

public record HouseholdMemberDto(
    Guid Id,
    Guid TenantId,
    Guid HouseholdId,
    string Name,
    bool IsAdult,
    string? AgeBand,
    DateOnly? BirthDate,
    string? DietaryNotes,
    string[] DietaryTags,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool IsDeleted,
    DateTimeOffset? DeletedAt,
    Guid? DeletedByUserId);
