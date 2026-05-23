using Gatherstead.Api.Contracts.EventAttributes;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Security;
using Gatherstead.Api.Services.EventAttributes;
using Gatherstead.Api.Services.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gatherstead.Api.Controllers;

[ApiController]
[Authorize]
[RequireTenantAccess]
[Route("api/tenants/{tenantId:guid}/events/{eventId:guid}/attributes")]
public class EventAttributesController : ControllerBase
{
    private readonly IEventAttributeService _attributeService;

    public EventAttributesController(IEventAttributeService attributeService)
    {
        _attributeService = attributeService ?? throw new ArgumentNullException(nameof(attributeService));
    }

    [HttpGet]
    public async Task<ActionResult<BaseEntityResponse<IReadOnlyCollection<EventAttributeDto>>>> GetAttributes(
        Guid tenantId,
        Guid eventId,
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

        var response = await _attributeService.ListAsync(tenantId, eventId, parsedIds, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        return Ok(response);
    }

    [HttpGet("{attributeId:guid}")]
    public async Task<ActionResult<EventAttributeResponse>> GetAttribute(Guid tenantId, Guid eventId, Guid attributeId, CancellationToken cancellationToken)
    {
        var response = await _attributeService.GetAsync(tenantId, eventId, attributeId, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }

    [HttpPost]
    public async Task<ActionResult<EventAttributeResponse>> CreateAttribute(Guid tenantId, Guid eventId, [FromBody] CreateEventAttributeRequest request, CancellationToken cancellationToken)
    {
        var response = await _attributeService.CreateAsync(tenantId, eventId, request, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        return CreatedAtAction(
            nameof(GetAttribute),
            new { tenantId, eventId, attributeId = response.Entity?.Id },
            response);
    }

    [HttpPut("{attributeId:guid}")]
    public async Task<ActionResult<EventAttributeResponse>> UpdateAttribute(Guid tenantId, Guid eventId, Guid attributeId, [FromBody] UpdateEventAttributeRequest request, CancellationToken cancellationToken)
    {
        var response = await _attributeService.UpdateAsync(tenantId, eventId, attributeId, request, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }

    [HttpDelete("{attributeId:guid}")]
    public async Task<ActionResult<EventAttributeResponse>> DeleteAttribute(Guid tenantId, Guid eventId, Guid attributeId, CancellationToken cancellationToken)
    {
        var response = await _attributeService.DeleteAsync(tenantId, eventId, attributeId, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }
}
