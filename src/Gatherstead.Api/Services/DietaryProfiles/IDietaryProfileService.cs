using Gatherstead.Api.Contracts.DietaryProfiles;
using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Services.DietaryProfiles;

public interface IDietaryProfileService
{
    Task<BaseEntityResponse<IReadOnlyCollection<DietaryProfileDto>>> ListAsync(
        Guid tenantId,
        IEnumerable<Guid>? memberIds = null,
        CancellationToken cancellationToken = default);

    Task<DietaryProfileResponse> GetAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        CancellationToken cancellationToken = default);

    Task<DietaryProfileResponse> UpsertAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        UpsertDietaryProfileRequest request,
        CancellationToken cancellationToken = default);

    Task<DietaryProfileResponse> DeleteAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        CancellationToken cancellationToken = default);
}
