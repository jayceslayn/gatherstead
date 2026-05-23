using Gatherstead.Api.Contracts.TenantAttributes;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Security;
using Gatherstead.Api.Services.TenantAttributes;
using Gatherstead.Api.Services.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gatherstead.Api.Controllers;

[ApiController]
[Authorize]
[RequireTenantAccess]
[Route("api/tenants/{tenantId:guid}/attributes")]
public class TenantAttributesController : ControllerBase
{
    private readonly ITenantAttributeService _attributeService;

    public TenantAttributesController(ITenantAttributeService attributeService)
    {
        _attributeService = attributeService ?? throw new ArgumentNullException(nameof(attributeService));
    }

    [HttpGet]
    public async Task<ActionResult<BaseEntityResponse<IReadOnlyCollection<TenantAttributeDto>>>> GetAttributes(
        Guid tenantId,
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

        var response = await _attributeService.ListAsync(tenantId, parsedIds, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        return Ok(response);
    }

    [HttpGet("{attributeId:guid}")]
    public async Task<ActionResult<TenantAttributeResponse>> GetAttribute(Guid tenantId, Guid attributeId, CancellationToken cancellationToken)
    {
        var response = await _attributeService.GetAsync(tenantId, attributeId, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }

    [HttpPost]
    public async Task<ActionResult<TenantAttributeResponse>> CreateAttribute(Guid tenantId, [FromBody] CreateTenantAttributeRequest request, CancellationToken cancellationToken)
    {
        var response = await _attributeService.CreateAsync(tenantId, request, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        return CreatedAtAction(
            nameof(GetAttribute),
            new { tenantId, attributeId = response.Entity?.Id },
            response);
    }

    [HttpPut("{attributeId:guid}")]
    public async Task<ActionResult<TenantAttributeResponse>> UpdateAttribute(Guid tenantId, Guid attributeId, [FromBody] UpdateTenantAttributeRequest request, CancellationToken cancellationToken)
    {
        var response = await _attributeService.UpdateAsync(tenantId, attributeId, request, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }

    [HttpDelete("{attributeId:guid}")]
    public async Task<ActionResult<TenantAttributeResponse>> DeleteAttribute(Guid tenantId, Guid attributeId, CancellationToken cancellationToken)
    {
        var response = await _attributeService.DeleteAsync(tenantId, attributeId, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }
}
