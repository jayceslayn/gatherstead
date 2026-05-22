using Gatherstead.Api.Contracts.HouseholdUsers;
using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Services.HouseholdUsers;

public interface IHouseholdUserService
{
    Task<BaseEntityResponse<IReadOnlyCollection<HouseholdUserDto>>> ListAsync(
        Guid tenantId,
        Guid householdId,
        CancellationToken cancellationToken = default);

    Task<HouseholdUserResponse> UpsertAsync(
        Guid tenantId,
        Guid householdId,
        Guid userId,
        UpsertHouseholdUserRequest request,
        CancellationToken cancellationToken = default);

    Task<HouseholdUserResponse> DeleteAsync(
        Guid tenantId,
        Guid householdId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
