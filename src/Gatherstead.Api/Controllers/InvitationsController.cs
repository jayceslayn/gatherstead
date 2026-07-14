using Gatherstead.Api.Contracts.Invitations;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Security;
using Gatherstead.Api.Services.Invitations;
using Gatherstead.Api.Services.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gatherstead.Api.Controllers;

[ApiController]
[Authorize]
[RequireTenantAccess]
[Route("api/tenants/{tenantId:guid}/invitations")]
public class InvitationsController : ControllerBase
{
    private readonly IInvitationService _invitationService;

    public InvitationsController(IInvitationService invitationService)
    {
        _invitationService = invitationService ?? throw new ArgumentNullException(nameof(invitationService));
    }

    [HttpGet]
    public async Task<ActionResult<BaseEntityResponse<IReadOnlyCollection<InvitationDto>>>> GetInvitations(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var response = await _invitationService.ListAsync(tenantId, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return this.ToErrorResult(response);

        return Ok(response);
    }

    [HttpPost]
    public async Task<ActionResult<InvitationResponse>> CreateInvitation(
        Guid tenantId,
        [FromBody] CreateInvitationRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _invitationService.CreateAsync(tenantId, request, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return this.ToErrorResult(response);

        return Ok(response);
    }

    [HttpDelete("{invitationId:guid}")]
    public async Task<ActionResult<InvitationResponse>> RevokeInvitation(
        Guid tenantId,
        Guid invitationId,
        CancellationToken cancellationToken)
    {
        var response = await _invitationService.RevokeAsync(tenantId, invitationId, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return this.ToErrorResult(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }
}
