using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Contracts.TenantUsers;

namespace Gatherstead.Api.Services.TenantUsers;

public interface ITenantUserService
{
    Task<BaseEntityResponse<IReadOnlyCollection<TenantUserDto>>> ListAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<TenantUserResponse> UpdateRoleAsync(
        Guid tenantId,
        Guid userId,
        UpdateTenantUserRoleRequest request,
        CancellationToken cancellationToken = default);

    Task<TenantUserResponse> SetLinkedMemberAsync(
        Guid tenantId,
        Guid userId,
        SetLinkedMemberRequest request,
        CancellationToken cancellationToken = default);
}
