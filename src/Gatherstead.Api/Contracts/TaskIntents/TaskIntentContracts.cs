using System.ComponentModel.DataAnnotations;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Contracts.TaskIntents;

public record TaskIntentDto(
    Guid Id,
    Guid TenantId,
    Guid TaskPlanId,
    Guid HouseholdMemberId,
    bool Volunteered,
    AuditInfo? Audit);

public class TaskIntentResponse : BaseEntityResponse<TaskIntentDto> { }

/// <summary>A member's volunteered task enriched with its plan day, task name and event context, for
/// the "My Upcoming Tasks" dashboard/feature widget.</summary>
public record MyTaskDto(
    Guid Id,
    Guid TaskPlanId,
    Guid HouseholdMemberId,
    string TaskName,
    Guid EventId,
    string EventName,
    DateOnly Day,
    TaskTimeSlot? TimeSlot,
    bool Completed,
    bool Volunteered);

public class UpsertTaskIntentRequest
{
    [Required]
    public Guid HouseholdMemberId { get; init; }

    public bool Volunteered { get; init; }
}

public class BulkUpsertTaskIntentItem
{
    [Required]
    public Guid TaskPlanId { get; init; }

    [Required]
    public Guid HouseholdMemberId { get; init; }

    public bool Volunteered { get; init; }
}

public class BulkUpsertTaskIntentRequest
{
    [Required]
    public IReadOnlyList<BulkUpsertTaskIntentItem> Items { get; init; } = Array.Empty<BulkUpsertTaskIntentItem>();
}
