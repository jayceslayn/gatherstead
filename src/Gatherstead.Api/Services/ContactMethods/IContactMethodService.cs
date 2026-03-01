using Gatherstead.Api.Contracts.ContactMethods;
using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Services.ContactMethods;

public interface IContactMethodService
{
    Task<BaseEntityResponse<IReadOnlyCollection<ContactMethodDto>>> ListAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        IEnumerable<Guid>? ids = null,
        CancellationToken cancellationToken = default);

    Task<ContactMethodResponse> GetAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        Guid contactMethodId,
        CancellationToken cancellationToken = default);

    Task<ContactMethodResponse> CreateAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        CreateContactMethodRequest request,
        CancellationToken cancellationToken = default);

    Task<ContactMethodResponse> UpdateAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        Guid contactMethodId,
        UpdateContactMethodRequest request,
        CancellationToken cancellationToken = default);

    Task<ContactMethodResponse> DeleteAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        Guid contactMethodId,
        CancellationToken cancellationToken = default);
}
