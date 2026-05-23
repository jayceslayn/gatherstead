using Gatherstead.Api.Contracts.MemberAttributes;
using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Services.MemberAttributes;

public interface IHouseholdMemberAttributeService
{
    Task<BaseEntityResponse<IReadOnlyCollection<HouseholdMemberAttributeDto>>> ListAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        IEnumerable<Guid>? ids = null,
        CancellationToken cancellationToken = default);

    Task<HouseholdMemberAttributeResponse> GetAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        Guid attributeId,
        CancellationToken cancellationToken = default);

    Task<HouseholdMemberAttributeResponse> CreateAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        CreateHouseholdMemberAttributeRequest request,
        CancellationToken cancellationToken = default);

    Task<HouseholdMemberAttributeResponse> UpdateAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        Guid attributeId,
        UpdateHouseholdMemberAttributeRequest request,
        CancellationToken cancellationToken = default);

    Task<HouseholdMemberAttributeResponse> DeleteAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        Guid attributeId,
        CancellationToken cancellationToken = default);
}
