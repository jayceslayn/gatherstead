namespace Gatherstead.Api.Security;

/// <summary>
/// Provides the platform-level App Admin status of the currently authenticated user.
/// </summary>
public interface IAppAdminContext
{
    /// <summary>
    /// Returns true if the current user is an App Admin, false if not, or null if no user is authenticated.
    /// Result is cached per-request in HttpContext.Items.
    /// </summary>
    Task<bool?> IsAppAdminAsync(CancellationToken ct = default);
}
