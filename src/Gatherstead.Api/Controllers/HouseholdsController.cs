using Gatherstead.Api.Contracts.Households;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Security;
using Gatherstead.Api.Services.Households;
using Gatherstead.Api.Services.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gatherstead.Api.Controllers;

[ApiController]
[Authorize]
[RequireTenantAccess]
[Route("api/tenants/{tenantId:guid}/households")]
public class HouseholdsController : ControllerBase
{
    private readonly IHouseholdService _householdService;

    public HouseholdsController(IHouseholdService householdService)
    {
        _householdService = householdService ?? throw new ArgumentNullException(nameof(householdService));
    }

    [HttpGet]
    public async Task<ActionResult<BaseEntityResponse<IReadOnlyCollection<HouseholdDto>>>> GetHouseholds(Guid tenantId, CancellationToken cancellationToken)
    {
        var response = await _householdService.ListAsync(tenantId, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    [HttpGet("{householdId:guid}")]
    public async Task<ActionResult<HouseholdResponse>> GetHousehold(Guid tenantId, Guid householdId, CancellationToken cancellationToken)
    {
        var response = await _householdService.GetAsync(tenantId, householdId, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
        {
            return BadRequest(response);
        }

        if (response.Entity is null)
        {
            return NotFound(response);
        }

        return Ok(response);
    }

    [HttpPost]
    public async Task<ActionResult<HouseholdResponse>> CreateHousehold(Guid tenantId, [FromBody] CreateHouseholdRequest request, CancellationToken cancellationToken)
    {
        var response = await _householdService.CreateAsync(tenantId, request, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
        {
            return BadRequest(response);
        }

        return CreatedAtAction(
            nameof(GetHousehold),
            new { tenantId, householdId = response.Entity?.Id },
            response);
    }

    [HttpPut("{householdId:guid}")]
    public async Task<ActionResult<HouseholdResponse>> UpdateHousehold(Guid tenantId, Guid householdId, [FromBody] UpdateHouseholdRequest request, CancellationToken cancellationToken)
    {
        var response = await _householdService.UpdateAsync(tenantId, householdId, request, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
        {
            return BadRequest(response);
        }

        if (response.Entity is null)
        {
            return NotFound(response);
        }

        return Ok(response);
    }

    [HttpDelete("{householdId:guid}")]
    public async Task<ActionResult<HouseholdResponse>> DeleteHousehold(Guid tenantId, Guid householdId, CancellationToken cancellationToken)
    {
        var response = await _householdService.DeleteAsync(tenantId, householdId, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
        {
            return BadRequest(response);
        }

        if (response.Entity is null)
        {
            return NotFound(response);
        }

        return Ok(response);
    }

}
