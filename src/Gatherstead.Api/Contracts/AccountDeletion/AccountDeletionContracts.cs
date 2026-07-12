using System.ComponentModel.DataAnnotations;
using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Contracts.AccountDeletion;

/// <summary>Entity payload when an account was erased (200).</summary>
public record AccountDeletionResultDto(
    [property: Required] Guid UserId,
    [property: Required] int TenantsPurged,
    [property: Required] int MembershipsRemoved,
    [property: Required] string DirectoryOutcome);

/// <summary>
/// Envelope for the deletion endpoints. Failures carry a coded message the frontend localizes —
/// notably <see cref="ErrorCode.ACCOUNT_SOLE_OWNER"/> (409) with the blocking group names in
/// <c>params.groups</c>.
/// </summary>
public class AccountDeletionResponse : BaseEntityResponse<AccountDeletionResultDto> { }
