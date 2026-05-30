using Gatherstead.Api.Contracts.Invitations;

namespace Gatherstead.Api.Services.Provisioning;

public interface IUserProvisioningService
{
    /// <summary>
    /// Ensures an internal <c>User</c> row exists for the authenticated caller (creating it on
    /// first login), refreshes their email, and auto-claims any pending invitations matching that
    /// email. Returns the resolved user id and the tenants they now belong to.
    /// </summary>
    Task<UserBootstrapResponse> BootstrapAsync(CancellationToken cancellationToken = default);
}
