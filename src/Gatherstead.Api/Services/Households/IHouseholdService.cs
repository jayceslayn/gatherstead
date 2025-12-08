using Gatherstead.Api.Contracts.Households;
using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Services.Households;

public interface IHouseholdService
{
    Task<BaseEntityResponse<IReadOnlyCollection<HouseholdDto>>> ListAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<HouseholdResponse> GetAsync(Guid tenantId, Guid householdId, CancellationToken cancellationToken = default);
    Task<HouseholdResponse> CreateAsync(Guid tenantId, CreateHouseholdRequest request, CancellationToken cancellationToken = default);
    Task<HouseholdResponse> UpdateAsync(Guid tenantId, Guid householdId, UpdateHouseholdRequest request, CancellationToken cancellationToken = default);
    Task<HouseholdResponse> DeleteAsync(Guid tenantId, Guid householdId, CancellationToken cancellationToken = default);
}
