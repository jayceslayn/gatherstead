using Gatherstead.Api.Contracts.MealAttendance;
using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Services.MealAttendance;

public interface IMealAttendanceService
{
    Task<BaseEntityResponse<IReadOnlyCollection<MealAttendanceDto>>> ListAsync(Guid tenantId, Guid planId, IEnumerable<Guid>? memberIds = null, CancellationToken cancellationToken = default);
    Task<MealAttendanceResponse> GetAsync(Guid tenantId, Guid planId, Guid attendanceId, CancellationToken cancellationToken = default);
    Task<MealAttendanceResponse> UpsertAsync(Guid tenantId, Guid planId, Guid householdId, UpsertMealAttendanceRequest request, CancellationToken cancellationToken = default);
    Task<MealAttendanceResponse> DeleteAsync(Guid tenantId, Guid planId, Guid attendanceId, CancellationToken cancellationToken = default);
}
