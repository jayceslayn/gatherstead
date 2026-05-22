using Gatherstead.Api.Contracts.TenantUsers;
using Gatherstead.Api.Security;
using Gatherstead.Api.Services.TenantUsers;
using Gatherstead.Api.Services.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gatherstead.Api.Controllers;

[ApiController]
[Authorize]
[RequireTenantAccess]
[Route("api/tenants/{tenantId:guid}/users")]
public class TenantUsersController : ControllerBase
{
    private readonly ITenantUserService _tenantUserService;

    public TenantUsersController(ITenantUserService tenantUserService)
    {
        _tenantUserService = tenantUserService ?? throw new ArgumentNullException(nameof(tenantUserService));
    }

    [HttpPut("{userId:guid}/linked-member")]
    public async Task<ActionResult<TenantUserResponse>> SetLinkedMember(
        Guid tenantId,
        Guid userId,
        [FromBody] SetLinkedMemberRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _tenantUserService.SetLinkedMemberAsync(tenantId, userId, request, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }
}
