using System;
using System.ComponentModel.DataAnnotations;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Contracts.MealAttendance;

public record MealAttendanceDto(
    Guid Id,
    Guid TenantId,
    Guid MealPlanId,
    Guid HouseholdMemberId,
    AttendanceStatus Status,
    bool BringOwnFood,
    string? Notes,
    AuditInfo? Audit);

public class MealAttendanceResponse : BaseEntityResponse<MealAttendanceDto> { }

public class UpsertMealAttendanceRequest
{
    [Required]
    public Guid HouseholdMemberId { get; init; }

    [Required]
    public AttendanceStatus Status { get; init; }

    public bool BringOwnFood { get; init; }
    public string? Notes { get; init; }
}

public class BulkUpsertMealAttendanceItem
{
    [Required]
    public Guid MealPlanId { get; init; }

    [Required]
    public Guid HouseholdMemberId { get; init; }

    [Required]
    public AttendanceStatus Status { get; init; }

    public bool BringOwnFood { get; init; }
    public string? Notes { get; init; }
}

public class BulkUpsertMealAttendanceRequest
{
    [Required]
    public IReadOnlyList<BulkUpsertMealAttendanceItem> Items { get; init; } = Array.Empty<BulkUpsertMealAttendanceItem>();
}
