using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Contracts.ChorePlans;

public record ChorePlanDto(
    Guid Id,
    Guid TenantId,
    Guid TemplateId,
    DateOnly Day,
    ChoreTimeSlot? TimeSlot,
    bool Completed,
    string? Notes,
    bool IsException,
    string? ExceptionReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool IsDeleted,
    DateTimeOffset? DeletedAt,
    Guid? DeletedByUserId);

public class ChorePlanResponse : BaseEntityResponse<ChorePlanDto> { }

public class UpdateChorePlanRequest
{
    public bool Completed { get; init; }
    public string? Notes { get; init; }
    public bool IsException { get; init; }
    public string? ExceptionReason { get; init; }
}
