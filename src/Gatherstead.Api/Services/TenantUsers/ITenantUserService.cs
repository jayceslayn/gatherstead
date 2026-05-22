using Gatherstead.Api.Contracts.TenantUsers;

namespace Gatherstead.Api.Services.TenantUsers;

public interface ITenantUserService
{
    Task<TenantUserResponse> SetLinkedMemberAsync(
        Guid tenantId,
        Guid userId,
        SetLinkedMemberRequest request,
        CancellationToken cancellationToken = default);
}
