using Gatherstead.Api.Contracts.HouseholdMembers;
using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Services.HouseholdMembers;

public interface IHouseholdMemberService
{
    Task<BaseEntityResponse<IReadOnlyCollection<HouseholdMemberDto>>> ListAsync(
        Guid tenantId,
        Guid householdId,
        IEnumerable<Guid>? ids = null,
        CancellationToken cancellationToken = default);

    Task<HouseholdMemberResponse> GetAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        CancellationToken cancellationToken = default);

    Task<HouseholdMemberResponse> CreateAsync(
        Guid tenantId,
        Guid householdId,
        CreateHouseholdMemberRequest request,
        CancellationToken cancellationToken = default);

    Task<HouseholdMemberResponse> UpdateAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        UpdateHouseholdMemberRequest request,
        CancellationToken cancellationToken = default);

    Task<HouseholdMemberResponse> DeleteAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        CancellationToken cancellationToken = default);
}
