using Gatherstead.Api.Contracts.AccountDeletion;
using Gatherstead.Api.Contracts.Invitations;
using Gatherstead.Api.Services.AccountDeletion;
using Gatherstead.Api.Services.Provisioning;
using Gatherstead.Api.Services.Validation;
using Gatherstead.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gatherstead.Api.Controllers;

/// <summary>
/// Endpoints scoped to the authenticated caller (not a specific tenant). Used at app startup to
/// provision the internal user and claim any pending invitations.
/// </summary>
[ApiController]
[Authorize]
[Route("api/me")]
public class MeController : ControllerBase
{
    private readonly IUserProvisioningService _provisioningService;
    private readonly IAccountDeletionService _accountDeletionService;
    private readonly ICurrentUserContext _currentUserContext;

    public MeController(
        IUserProvisioningService provisioningService,
        IAccountDeletionService accountDeletionService,
        ICurrentUserContext currentUserContext)
    {
        _provisioningService = provisioningService ?? throw new ArgumentNullException(nameof(provisioningService));
        _accountDeletionService = accountDeletionService ?? throw new ArgumentNullException(nameof(accountDeletionService));
        _currentUserContext = currentUserContext ?? throw new ArgumentNullException(nameof(currentUserContext));
    }

    [HttpPost("bootstrap")]
    public async Task<ActionResult<UserBootstrapResponse>> Bootstrap(CancellationToken cancellationToken)
    {
        var response = await _provisioningService.BootstrapAsync(cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return this.ToErrorResult(response);

        return Ok(response);
    }

    [HttpGet]
    public async Task<ActionResult<MeResponse>> Get(CancellationToken cancellationToken)
    {
        var response = await _provisioningService.GetMeAsync(cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return this.ToErrorResult(response);

        return Ok(response);
    }

    [HttpPut]
    public async Task<ActionResult<MeResponse>> Update([FromBody] UpdateMeRequest request, CancellationToken cancellationToken)
    {
        var response = await _provisioningService.UpdateDisplayNameAsync(request.DisplayName, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return this.ToErrorResult(response);

        return Ok(response);
    }

    /// <summary>
    /// Permanently deletes the authenticated caller's account and all their personal data. Returns 409
    /// when the caller is the sole Owner of a live shared group (they must transfer ownership, remove
    /// the members, or delete the group first).
    /// </summary>
    [HttpDelete]
    public async Task<ActionResult<AccountDeletionResponse>> Delete(CancellationToken cancellationToken)
    {
        var userId = _currentUserContext.UserId;
        if (!userId.HasValue)
            return Unauthorized();

        // The caller's own token is revoked with the account so the session dies server-side too.
        var callerTokenId = User.FindFirst("jti")?.Value;
        var result = await _accountDeletionService.DeleteUserAsync(userId.Value, userId.Value, callerTokenId, cancellationToken);
        return AccountDeletionActionResult.ToActionResult(this, result);
    }
}
