using Gatherstead.Api.Contracts.Events;
using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Services.Events;

public interface IEventService
{
    Task<BaseEntityResponse<IReadOnlyCollection<EventDto>>> ListAsync(Guid tenantId, IEnumerable<Guid>? ids = null, CancellationToken cancellationToken = default);
    Task<EventResponse> GetAsync(Guid tenantId, Guid eventId, CancellationToken cancellationToken = default);
    Task<EventResponse> CreateAsync(Guid tenantId, CreateEventRequest request, CancellationToken cancellationToken = default);
    Task<EventResponse> UpdateAsync(Guid tenantId, Guid eventId, UpdateEventRequest request, CancellationToken cancellationToken = default);
    Task<EventResponse> DeleteAsync(Guid tenantId, Guid eventId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Regenerates ChorePlan and MealPlan records for all templates on the event,
    /// reconciling the full event date range. Useful after template configuration changes.
    /// </summary>
    Task<EventResponse> SyncPlansAsync(Guid tenantId, Guid eventId, CancellationToken cancellationToken = default);
}
