namespace Gatherstead.Api.Services.AccountDeletion;

/// <summary>
/// Hard-erases a user: their account record, their own personal data (linked household member and its
/// dependents), their group memberships, invitations addressed to their email, and — where enabled —
/// their external-identity (Entra) account. This is a true deletion, not the soft-delete used
/// everywhere else, so it deliberately bypasses the soft-delete interceptor and global query filters.
/// </summary>
/// <remarks>
/// Guard rail: if the user is the sole Owner of a live group that still has other members, deletion
/// is refused (<see cref="AccountDeletionStatus.BlockedByOwnership"/>) so a shared group is never
/// orphaned — the user can transfer ownership, remove the members, or delete the group first.
/// Groups where the user is the only member, and groups the user already deleted as Owner, are
/// purged in full.
/// </remarks>
public interface IAccountDeletionService
{
    /// <param name="userId">The user to erase.</param>
    /// <param name="initiatedByUserId">
    /// Who initiated the erasure (the user themselves for self-service, or an app admin). Recorded on
    /// the append-only <c>AccountDeleted</c> security event.
    /// </param>
    /// <param name="callerTokenId">
    /// The <c>jti</c> of the session performing a self-service deletion, revoked server-side once the
    /// erasure is durable so the token dies with the account. Null for admin-initiated deletion (the
    /// target's tokens are unknown; the erasure tombstone blocks re-provisioning instead).
    /// </param>
    Task<AccountDeletionResult> DeleteUserAsync(
        Guid userId,
        Guid initiatedByUserId,
        string? callerTokenId = null,
        CancellationToken cancellationToken = default);
}
