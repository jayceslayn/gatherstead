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

    [HttpGet]
    public async Task<IActionResult> ListUsers(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var response = await _tenantUserService.ListAsync(tenantId, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return this.ToErrorResult(response);

        return Ok(response);
    }

    [HttpPut("{userId:guid}/role")]
    public async Task<ActionResult<TenantUserResponse>> UpdateRole(
        Guid tenantId,
        Guid userId,
        [FromBody] UpdateTenantUserRoleRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _tenantUserService.UpdateRoleAsync(tenantId, userId, request, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return this.ToErrorResult(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }

    [HttpDelete("{userId:guid}")]
    public async Task<ActionResult<TenantUserResponse>> RemoveUser(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var response = await _tenantUserService.RemoveAsync(tenantId, userId, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return this.ToErrorResult(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }

    [HttpGet("{userId:guid}/household-access")]
    public async Task<IActionResult> ListUserHouseholdAccess(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var response = await _tenantUserService.ListUserHouseholdAccessAsync(tenantId, userId, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return this.ToErrorResult(response);

        return Ok(response);
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
            return this.ToErrorResult(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var response = await _tenantUserService.GetCurrentTenantUserAsync(tenantId, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return this.ToErrorResult(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }
}
