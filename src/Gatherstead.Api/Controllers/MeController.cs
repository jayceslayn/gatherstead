using Gatherstead.Api.Contracts.Invitations;
using Gatherstead.Api.Services.Provisioning;
using Gatherstead.Api.Services.Validation;
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

    public MeController(IUserProvisioningService provisioningService)
    {
        _provisioningService = provisioningService ?? throw new ArgumentNullException(nameof(provisioningService));
    }

    [HttpPost("bootstrap")]
    public async Task<ActionResult<UserBootstrapResponse>> Bootstrap(CancellationToken cancellationToken)
    {
        var response = await _provisioningService.BootstrapAsync(cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        return Ok(response);
    }
}
