using System.ComponentModel.DataAnnotations;
using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Contracts.ChoreIntents;

public record ChoreIntentDto(
    Guid Id,
    Guid TenantId,
    Guid ChorePlanId,
    Guid HouseholdMemberId,
    bool Volunteered,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool IsDeleted,
    DateTimeOffset? DeletedAt,
    Guid? DeletedByUserId);

public class ChoreIntentResponse : BaseEntityResponse<ChoreIntentDto> { }

public class UpsertChoreIntentRequest
{
    [Required]
    public Guid HouseholdMemberId { get; init; }

    public bool Volunteered { get; init; }
}
