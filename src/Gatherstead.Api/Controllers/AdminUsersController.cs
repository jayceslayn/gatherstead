using Gatherstead.Api.Contracts.AccountDeletion;
using Gatherstead.Api.Security;
using Gatherstead.Api.Services.AccountDeletion;
using Gatherstead.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gatherstead.Api.Controllers;

/// <summary>
/// App-admin-only operations on users. Currently: operator-initiated account erasure, reusing the
/// same <see cref="IAccountDeletionService"/> as the self-service <c>DELETE /api/me</c> flow.
/// </summary>
[ApiController]
[Authorize]
[RequireAppAdmin]
[Route("api/admin/users")]
public class AdminUsersController : ControllerBase
{
    private readonly IAccountDeletionService _accountDeletionService;
    private readonly ICurrentUserContext _currentUserContext;

    public AdminUsersController(
        IAccountDeletionService accountDeletionService,
        ICurrentUserContext currentUserContext)
    {
        _accountDeletionService = accountDeletionService ?? throw new ArgumentNullException(nameof(accountDeletionService));
        _currentUserContext = currentUserContext ?? throw new ArgumentNullException(nameof(currentUserContext));
    }

    /// <summary>
    /// Permanently deletes the given user and all their personal data. Returns 409 when that user is
    /// the sole Owner of a live shared group (ownership must be transferred, the members removed, or
    /// the group deleted first).
    /// </summary>
    [HttpDelete("{userId:guid}")]
    public async Task<ActionResult<AccountDeletionResponse>> Delete(Guid userId, CancellationToken cancellationToken)
    {
        // No callerTokenId: the admin's own session must survive, and the target's tokens are
        // unknown — the erasure tombstone blocks a stale token from re-provisioning the account.
        var initiatedBy = _currentUserContext.UserId ?? Guid.Empty;
        var result = await _accountDeletionService.DeleteUserAsync(userId, initiatedBy, callerTokenId: null, cancellationToken);
        return AccountDeletionActionResult.ToActionResult(this, result);
    }
}
