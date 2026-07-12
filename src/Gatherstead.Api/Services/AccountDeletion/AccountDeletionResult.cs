using Gatherstead.Api.Services.Directory;

namespace Gatherstead.Api.Services.AccountDeletion;

public enum AccountDeletionStatus
{
    /// <summary>The account and all personal data were erased.</summary>
    Deleted = 0,
    /// <summary>No user row matched the id.</summary>
    NotFound = 1,
    /// <summary>The user is the sole Owner of one or more shared groups; deletion was refused.</summary>
    BlockedByOwnership = 2,
}

/// <summary>Result of an account-erasure attempt, mapped to an HTTP status by the controller.</summary>
public sealed class AccountDeletionResult
{
    public required AccountDeletionStatus Status { get; init; }
    public Guid UserId { get; init; }

    /// <summary>Groups blocking deletion when <see cref="Status"/> is <c>BlockedByOwnership</c>.</summary>
    public IReadOnlyList<Guid> BlockingTenantIds { get; init; } = [];

    /// <summary>Display names of the blocking groups, for the user-facing error message.</summary>
    public IReadOnlyList<string> BlockingTenantNames { get; init; } = [];

    /// <summary>Number of groups fully purged (the user was their only member).</summary>
    public int TenantsPurged { get; init; }

    /// <summary>Number of shared groups the user was merely removed from.</summary>
    public int MembershipsRemoved { get; init; }

    public DirectoryDeletionOutcome DirectoryOutcome { get; init; }

    public static AccountDeletionResult NotFound(Guid userId) =>
        new() { Status = AccountDeletionStatus.NotFound, UserId = userId };

    public static AccountDeletionResult Blocked(Guid userId, IReadOnlyList<Guid> blockingTenantIds, IReadOnlyList<string> blockingTenantNames) =>
        new()
        {
            Status = AccountDeletionStatus.BlockedByOwnership,
            UserId = userId,
            BlockingTenantIds = blockingTenantIds,
            BlockingTenantNames = blockingTenantNames,
        };
}
