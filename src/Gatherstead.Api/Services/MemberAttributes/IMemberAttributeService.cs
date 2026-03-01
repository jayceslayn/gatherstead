using Gatherstead.Api.Contracts.MemberAttributes;
using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Services.MemberAttributes;

public interface IMemberAttributeService
{
    Task<BaseEntityResponse<IReadOnlyCollection<MemberAttributeDto>>> ListAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        IEnumerable<Guid>? ids = null,
        CancellationToken cancellationToken = default);

    Task<MemberAttributeResponse> GetAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        Guid attributeId,
        CancellationToken cancellationToken = default);

    Task<MemberAttributeResponse> CreateAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        CreateMemberAttributeRequest request,
        CancellationToken cancellationToken = default);

    Task<MemberAttributeResponse> UpdateAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        Guid attributeId,
        UpdateMemberAttributeRequest request,
        CancellationToken cancellationToken = default);

    Task<MemberAttributeResponse> DeleteAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        Guid attributeId,
        CancellationToken cancellationToken = default);
}
