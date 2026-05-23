using Gatherstead.Api.Contracts.HouseholdAttributes;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Security;
using Gatherstead.Api.Services.HouseholdAttributes;
using Gatherstead.Api.Services.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gatherstead.Api.Controllers;

[ApiController]
[Authorize]
[RequireTenantAccess]
[Route("api/tenants/{tenantId:guid}/households/{householdId:guid}/attributes")]
public class HouseholdAttributesController : ControllerBase
{
    private readonly IHouseholdAttributeService _attributeService;

    public HouseholdAttributesController(IHouseholdAttributeService attributeService)
    {
        _attributeService = attributeService ?? throw new ArgumentNullException(nameof(attributeService));
    }

    [HttpGet]
    public async Task<ActionResult<BaseEntityResponse<IReadOnlyCollection<HouseholdAttributeDto>>>> GetAttributes(
        Guid tenantId,
        Guid householdId,
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

        var response = await _attributeService.ListAsync(tenantId, householdId, parsedIds, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        return Ok(response);
    }

    [HttpGet("{attributeId:guid}")]
    public async Task<ActionResult<HouseholdAttributeResponse>> GetAttribute(Guid tenantId, Guid householdId, Guid attributeId, CancellationToken cancellationToken)
    {
        var response = await _attributeService.GetAsync(tenantId, householdId, attributeId, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }

    [HttpPost]
    public async Task<ActionResult<HouseholdAttributeResponse>> CreateAttribute(Guid tenantId, Guid householdId, [FromBody] CreateHouseholdAttributeRequest request, CancellationToken cancellationToken)
    {
        var response = await _attributeService.CreateAsync(tenantId, householdId, request, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        return CreatedAtAction(
            nameof(GetAttribute),
            new { tenantId, householdId, attributeId = response.Entity?.Id },
            response);
    }

    [HttpPut("{attributeId:guid}")]
    public async Task<ActionResult<HouseholdAttributeResponse>> UpdateAttribute(Guid tenantId, Guid householdId, Guid attributeId, [FromBody] UpdateHouseholdAttributeRequest request, CancellationToken cancellationToken)
    {
        var response = await _attributeService.UpdateAsync(tenantId, householdId, attributeId, request, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }

    [HttpDelete("{attributeId:guid}")]
    public async Task<ActionResult<HouseholdAttributeResponse>> DeleteAttribute(Guid tenantId, Guid householdId, Guid attributeId, CancellationToken cancellationToken)
    {
        var response = await _attributeService.DeleteAsync(tenantId, householdId, attributeId, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }
}
