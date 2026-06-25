using Gatherstead.Api.Contracts.AccommodationIntents;
using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Services.AccommodationIntents;

public interface IAccommodationIntentService
{
    Task<BaseEntityResponse<IReadOnlyCollection<AccommodationIntentDto>>> ListAsync(Guid tenantId, Guid accommodationId, IEnumerable<Guid>? memberIds = null, CancellationToken cancellationToken = default);

    /// <summary>Lists stays across all accommodations in the tenant, enriched with accommodation/property
    /// names. Optionally filtered to specific members and to stays ending on or after <paramref name="fromNight"/>.</summary>
    Task<BaseEntityResponse<IReadOnlyCollection<MyStayDto>>> ListForTenantAsync(Guid tenantId, IEnumerable<Guid>? memberIds = null, DateOnly? fromNight = null, CancellationToken cancellationToken = default);
    Task<AccommodationIntentResponse> GetAsync(Guid tenantId, Guid accommodationId, Guid intentId, CancellationToken cancellationToken = default);
    Task<AccommodationIntentResponse> CreateAsync(Guid tenantId, Guid accommodationId, Guid householdId, CreateAccommodationIntentRequest request, CancellationToken cancellationToken = default);
    Task<AccommodationIntentResponse> UpdateAsync(Guid tenantId, Guid accommodationId, Guid intentId, UpdateAccommodationIntentRequest request, CancellationToken cancellationToken = default);
    Task<AccommodationIntentResponse> DeleteAsync(Guid tenantId, Guid accommodationId, Guid intentId, CancellationToken cancellationToken = default);
}
