namespace Gatherstead.Api.Services.Directory;

/// <summary>Outcome of attempting to delete a user's account in the external identity directory.</summary>
public enum DirectoryDeletionOutcome
{
    /// <summary>Directory management is disabled by configuration; the account was left untouched.</summary>
    Skipped = 0,
    /// <summary>The directory account was deleted.</summary>
    Deleted = 1,
    /// <summary>No matching directory account was found (already gone / never existed).</summary>
    NotFound = 2,
    /// <summary>The deletion was attempted but failed; the operator must remove the account manually.</summary>
    Failed = 3,
}

/// <summary>
/// Deletes a user's account in the external identity provider (Microsoft Entra External ID), so a
/// "right to erasure" removes the directory identity as well as our application data. Implementations
/// never throw: a failure is reported as <see cref="DirectoryDeletionOutcome.Failed"/> for operator
/// follow-up rather than aborting the (already-committed) application-side erasure.
/// </summary>
public interface IDirectoryAccountService
{
    Task<DirectoryDeletionOutcome> DeleteUserAsync(string externalId, CancellationToken cancellationToken = default);
}
