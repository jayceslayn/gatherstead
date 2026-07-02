using Gatherstead.Api.Contracts.MealAttendance;
using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Services.MealAttendance;

public interface IMealAttendanceService
{
    Task<BaseEntityResponse<IReadOnlyCollection<MealAttendanceDto>>> ListAsync(Guid tenantId, Guid planId, IEnumerable<Guid>? memberIds = null, CancellationToken cancellationToken = default);

    /// <summary>Lists all meal attendance across every meal plan of a single event in one call, so the
    /// client avoids one request per plan. Optionally filtered to specific members.</summary>
    Task<BaseEntityResponse<IReadOnlyCollection<MealAttendanceDto>>> ListForEventAsync(Guid tenantId, Guid eventId, IEnumerable<Guid>? memberIds = null, CancellationToken cancellationToken = default);
    Task<MealAttendanceResponse> GetAsync(Guid tenantId, Guid planId, Guid attendanceId, CancellationToken cancellationToken = default);
    Task<MealAttendanceResponse> UpsertAsync(Guid tenantId, Guid planId, Guid householdId, UpsertMealAttendanceRequest request, CancellationToken cancellationToken = default);
    Task<BulkUpsertResponse<MealAttendanceDto>> BulkUpsertAsync(Guid tenantId, Guid eventId, BulkUpsertMealAttendanceRequest request, CancellationToken cancellationToken = default);
    Task<MealAttendanceResponse> DeleteAsync(Guid tenantId, Guid planId, Guid attendanceId, CancellationToken cancellationToken = default);
}
