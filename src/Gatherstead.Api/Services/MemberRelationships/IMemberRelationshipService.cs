using Gatherstead.Api.Contracts.MemberRelationships;
using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Services.MemberRelationships;

public interface IMemberRelationshipService
{
    Task<BaseEntityResponse<IReadOnlyCollection<MemberRelationshipDto>>> ListAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        IEnumerable<Guid>? ids = null,
        CancellationToken cancellationToken = default);

    Task<MemberRelationshipResponse> GetAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        Guid relationshipId,
        CancellationToken cancellationToken = default);

    Task<MemberRelationshipResponse> CreateAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        CreateMemberRelationshipRequest request,
        CancellationToken cancellationToken = default);

    Task<MemberRelationshipResponse> UpdateAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        Guid relationshipId,
        UpdateMemberRelationshipRequest request,
        CancellationToken cancellationToken = default);

    Task<MemberRelationshipResponse> DeleteAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        Guid relationshipId,
        CancellationToken cancellationToken = default);
}
