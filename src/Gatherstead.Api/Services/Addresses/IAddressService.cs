using Gatherstead.Api.Contracts.Addresses;
using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Services.Addresses;

public interface IAddressService
{
    Task<BaseEntityResponse<IReadOnlyCollection<AddressDto>>> ListAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        IEnumerable<Guid>? ids = null,
        CancellationToken cancellationToken = default);

    Task<AddressResponse> GetAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        Guid addressId,
        CancellationToken cancellationToken = default);

    Task<AddressResponse> CreateAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        CreateAddressRequest request,
        CancellationToken cancellationToken = default);

    Task<AddressResponse> UpdateAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        Guid addressId,
        UpdateAddressRequest request,
        CancellationToken cancellationToken = default);

    Task<AddressResponse> DeleteAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        Guid addressId,
        CancellationToken cancellationToken = default);
}
