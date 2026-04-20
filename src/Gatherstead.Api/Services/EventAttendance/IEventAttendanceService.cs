using Gatherstead.Api.Contracts.EventAttendance;
using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Services.EventAttendance;

public interface IEventAttendanceService
{
    Task<BaseEntityResponse<IReadOnlyCollection<EventAttendanceDto>>> ListAsync(Guid tenantId, Guid eventId, IEnumerable<Guid>? memberIds = null, CancellationToken cancellationToken = default);
    Task<EventAttendanceResponse> GetAsync(Guid tenantId, Guid eventId, Guid attendanceId, CancellationToken cancellationToken = default);
    Task<EventAttendanceResponse> UpsertAsync(Guid tenantId, Guid eventId, Guid householdId, UpsertEventAttendanceRequest request, CancellationToken cancellationToken = default);
    Task<EventAttendanceResponse> DeleteAsync(Guid tenantId, Guid eventId, Guid attendanceId, CancellationToken cancellationToken = default);
}
