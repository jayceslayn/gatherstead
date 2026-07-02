using Gatherstead.Api.Contracts.TaskIntents;
using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Services.TaskIntents;

public interface ITaskIntentService
{
    Task<BaseEntityResponse<IReadOnlyCollection<TaskIntentDto>>> ListAsync(Guid tenantId, Guid planId, IEnumerable<Guid>? memberIds = null, CancellationToken cancellationToken = default);

    /// <summary>Lists volunteered task intents across all events in the tenant, enriched with task/event
    /// context. Optionally filtered to specific members and to plans on or after <paramref name="fromDay"/>.</summary>
    Task<BaseEntityResponse<IReadOnlyCollection<MyTaskDto>>> ListForMemberAsync(Guid tenantId, IEnumerable<Guid>? memberIds = null, DateOnly? fromDay = null, CancellationToken cancellationToken = default);
    /// <summary>Lists all task intents across every task plan of a single event in one call, so the
    /// client avoids one request per plan. Optionally filtered to specific members.</summary>
    Task<BaseEntityResponse<IReadOnlyCollection<TaskIntentDto>>> ListForEventAsync(Guid tenantId, Guid eventId, IEnumerable<Guid>? memberIds = null, CancellationToken cancellationToken = default);
    Task<TaskIntentResponse> GetAsync(Guid tenantId, Guid planId, Guid intentId, CancellationToken cancellationToken = default);
    Task<TaskIntentResponse> UpsertAsync(Guid tenantId, Guid planId, Guid householdId, UpsertTaskIntentRequest request, CancellationToken cancellationToken = default);
    Task<BulkUpsertResponse<TaskIntentDto>> BulkUpsertAsync(Guid tenantId, Guid eventId, BulkUpsertTaskIntentRequest request, CancellationToken cancellationToken = default);
    Task<TaskIntentResponse> DeleteAsync(Guid tenantId, Guid planId, Guid intentId, CancellationToken cancellationToken = default);
}
