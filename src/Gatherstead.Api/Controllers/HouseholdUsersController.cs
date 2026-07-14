using Gatherstead.Api.Contracts.HouseholdUsers;
using Gatherstead.Api.Security;
using Gatherstead.Api.Services.HouseholdUsers;
using Gatherstead.Api.Services.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gatherstead.Api.Controllers;

[ApiController]
[Authorize]
[RequireTenantAccess]
[Route("api/tenants/{tenantId:guid}/households/{householdId:guid}/users")]
public class HouseholdUsersController : ControllerBase
{
    private readonly IHouseholdUserService _householdUserService;

    public HouseholdUsersController(IHouseholdUserService householdUserService)
    {
        _householdUserService = householdUserService ?? throw new ArgumentNullException(nameof(householdUserService));
    }

    [HttpGet]
    public async Task<IActionResult> ListUsers(
        Guid tenantId,
        Guid householdId,
        CancellationToken cancellationToken)
    {
        var response = await _householdUserService.ListAsync(tenantId, householdId, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return this.ToErrorResult(response);

        return Ok(response);
    }

    [HttpPut("{userId:guid}")]
    public async Task<ActionResult<HouseholdUserResponse>> UpsertUser(
        Guid tenantId,
        Guid householdId,
        Guid userId,
        [FromBody] UpsertHouseholdUserRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _householdUserService.UpsertAsync(tenantId, householdId, userId, request, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return this.ToErrorResult(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }

    [HttpDelete("{userId:guid}")]
    public async Task<ActionResult<HouseholdUserResponse>> DeleteUser(
        Guid tenantId,
        Guid householdId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var response = await _householdUserService.DeleteAsync(tenantId, householdId, userId, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return this.ToErrorResult(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }
}
