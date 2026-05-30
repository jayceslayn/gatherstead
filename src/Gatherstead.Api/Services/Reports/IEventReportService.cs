using Gatherstead.Api.Contracts.Reports;

namespace Gatherstead.Api.Services.Reports;

public interface IEventReportService
{
    Task<EventReportResponse> GetEventMealReportAsync(
        Guid tenantId,
        Guid eventId,
        CancellationToken cancellationToken = default);
}
