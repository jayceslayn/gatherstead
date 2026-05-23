using Gatherstead.Api.Contracts.HouseholdAttributes;
using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Services.HouseholdAttributes;

public interface IHouseholdAttributeService
{
    Task<BaseEntityResponse<IReadOnlyCollection<HouseholdAttributeDto>>> ListAsync(
        Guid tenantId,
        Guid householdId,
        IEnumerable<Guid>? ids = null,
        CancellationToken cancellationToken = default);

    Task<HouseholdAttributeResponse> GetAsync(
        Guid tenantId,
        Guid householdId,
        Guid attributeId,
        CancellationToken cancellationToken = default);

    Task<HouseholdAttributeResponse> CreateAsync(
        Guid tenantId,
        Guid householdId,
        CreateHouseholdAttributeRequest request,
        CancellationToken cancellationToken = default);

    Task<HouseholdAttributeResponse> UpdateAsync(
        Guid tenantId,
        Guid householdId,
        Guid attributeId,
        UpdateHouseholdAttributeRequest request,
        CancellationToken cancellationToken = default);

    Task<HouseholdAttributeResponse> DeleteAsync(
        Guid tenantId,
        Guid householdId,
        Guid attributeId,
        CancellationToken cancellationToken = default);
}
