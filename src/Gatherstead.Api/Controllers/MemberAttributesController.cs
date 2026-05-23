using Gatherstead.Api.Contracts.MemberAttributes;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Security;
using Gatherstead.Api.Services.MemberAttributes;
using Gatherstead.Api.Services.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gatherstead.Api.Controllers;

[ApiController]
[Authorize]
[RequireTenantAccess]
[Route("api/tenants/{tenantId:guid}/households/{householdId:guid}/members/{memberId:guid}/attributes")]
public class HouseholdMemberAttributesController : ControllerBase
{
    private readonly IHouseholdMemberAttributeService _attributeService;

    public HouseholdMemberAttributesController(IHouseholdMemberAttributeService attributeService)
    {
        _attributeService = attributeService ?? throw new ArgumentNullException(nameof(attributeService));
    }

    [HttpGet]
    public async Task<ActionResult<BaseEntityResponse<IReadOnlyCollection<HouseholdMemberAttributeDto>>>> GetAttributes(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        [FromQuery] string? ids,
        CancellationToken cancellationToken)
    {
        IEnumerable<Guid>? parsedIds = null;
        if (!string.IsNullOrWhiteSpace(ids))
        {
            var idList = new List<Guid>();
            foreach (var segment in ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!Guid.TryParse(segment, out var parsed))
                    return BadRequest(new { error = $"Invalid attribute identifier: '{segment}'." });
                idList.Add(parsed);
            }
            parsedIds = idList;
        }

        var response = await _attributeService.ListAsync(tenantId, householdId, memberId, parsedIds, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        return Ok(response);
    }

    [HttpGet("{attributeId:guid}")]
    public async Task<ActionResult<HouseholdMemberAttributeResponse>> GetAttribute(Guid tenantId, Guid householdId, Guid memberId, Guid attributeId, CancellationToken cancellationToken)
    {
        var response = await _attributeService.GetAsync(tenantId, householdId, memberId, attributeId, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }

    [HttpPost]
    public async Task<ActionResult<HouseholdMemberAttributeResponse>> CreateAttribute(Guid tenantId, Guid householdId, Guid memberId, [FromBody] CreateHouseholdMemberAttributeRequest request, CancellationToken cancellationToken)
    {
        var response = await _attributeService.CreateAsync(tenantId, householdId, memberId, request, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        return CreatedAtAction(
            nameof(GetAttribute),
            new { tenantId, householdId, memberId, attributeId = response.Entity?.Id },
            response);
    }

    [HttpPut("{attributeId:guid}")]
    public async Task<ActionResult<HouseholdMemberAttributeResponse>> UpdateAttribute(Guid tenantId, Guid householdId, Guid memberId, Guid attributeId, [FromBody] UpdateHouseholdMemberAttributeRequest request, CancellationToken cancellationToken)
    {
        var response = await _attributeService.UpdateAsync(tenantId, householdId, memberId, attributeId, request, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }

    [HttpDelete("{attributeId:guid}")]
    public async Task<ActionResult<HouseholdMemberAttributeResponse>> DeleteAttribute(Guid tenantId, Guid householdId, Guid memberId, Guid attributeId, CancellationToken cancellationToken)
    {
        var response = await _attributeService.DeleteAsync(tenantId, householdId, memberId, attributeId, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }
}
