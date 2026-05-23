using Gatherstead.Api.Contracts.EventAttributes;
using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Services.EventAttributes;

public interface IEventAttributeService
{
    Task<BaseEntityResponse<IReadOnlyCollection<EventAttributeDto>>> ListAsync(
        Guid tenantId,
        Guid eventId,
        IEnumerable<Guid>? ids = null,
        CancellationToken cancellationToken = default);

    Task<EventAttributeResponse> GetAsync(
        Guid tenantId,
        Guid eventId,
        Guid attributeId,
        CancellationToken cancellationToken = default);

    Task<EventAttributeResponse> CreateAsync(
        Guid tenantId,
        Guid eventId,
        CreateEventAttributeRequest request,
        CancellationToken cancellationToken = default);

    Task<EventAttributeResponse> UpdateAsync(
        Guid tenantId,
        Guid eventId,
        Guid attributeId,
        UpdateEventAttributeRequest request,
        CancellationToken cancellationToken = default);

    Task<EventAttributeResponse> DeleteAsync(
        Guid tenantId,
        Guid eventId,
        Guid attributeId,
        CancellationToken cancellationToken = default);
}
