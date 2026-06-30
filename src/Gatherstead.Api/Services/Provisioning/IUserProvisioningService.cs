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

    /// <summary>
    /// Returns the authenticated caller's own profile (id, email, display name).
    /// </summary>
    Task<MeResponse> GetMeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the authenticated caller's editable display name and returns the refreshed profile.
    /// </summary>
    Task<MeResponse> UpdateDisplayNameAsync(string displayName, CancellationToken cancellationToken = default);
}
