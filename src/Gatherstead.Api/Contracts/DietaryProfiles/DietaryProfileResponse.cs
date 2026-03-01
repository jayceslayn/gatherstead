using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Contracts.DietaryProfiles;

public class DietaryProfileResponse : BaseEntityResponse<DietaryProfileDto>
{
}

public record DietaryProfileDto(
    Guid Id,
    Guid TenantId,
    Guid HouseholdMemberId,
    string PreferredDiet,
    string[] Allergies,
    string[] Restrictions,
    string? Notes,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool IsDeleted,
    DateTimeOffset? DeletedAt,
    Guid? DeletedByUserId);
