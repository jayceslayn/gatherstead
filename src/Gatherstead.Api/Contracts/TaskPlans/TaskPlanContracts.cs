using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Contracts.TaskPlans;

public record TaskPlanDto(
    Guid Id,
    Guid TenantId,
    Guid TemplateId,
    DateOnly Day,
    TaskTimeSlot? TimeSlot,
    bool Completed,
    string? Notes,
    bool IsException,
    string? ExceptionReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool IsDeleted,
    DateTimeOffset? DeletedAt,
    Guid? DeletedByUserId);

public class TaskPlanResponse : BaseEntityResponse<TaskPlanDto> { }

public class UpdateTaskPlanRequest
{
    public bool Completed { get; init; }
    public string? Notes { get; init; }
    public bool IsException { get; init; }
    public string? ExceptionReason { get; init; }
}
