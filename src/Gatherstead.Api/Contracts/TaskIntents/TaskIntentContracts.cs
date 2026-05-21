using System.ComponentModel.DataAnnotations;
using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Contracts.TaskIntents;

public record TaskIntentDto(
    Guid Id,
    Guid TenantId,
    Guid TaskPlanId,
    Guid HouseholdMemberId,
    bool Volunteered,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool IsDeleted,
    DateTimeOffset? DeletedAt,
    Guid? DeletedByUserId);

public class TaskIntentResponse : BaseEntityResponse<TaskIntentDto> { }

public class UpsertTaskIntentRequest
{
    [Required]
    public Guid HouseholdMemberId { get; init; }

    public bool Volunteered { get; init; }
}
