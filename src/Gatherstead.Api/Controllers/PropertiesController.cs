using Gatherstead.Api.Contracts.Properties;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Security;
using Gatherstead.Api.Services.Properties;
using Gatherstead.Api.Services.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gatherstead.Api.Controllers;

[ApiController]
[Authorize]
[RequireTenantAccess]
[Route("api/tenants/{tenantId:guid}/properties")]
public class PropertiesController : ControllerBase
{
    private readonly IPropertyService _propertyService;

    public PropertiesController(IPropertyService propertyService)
    {
        _propertyService = propertyService ?? throw new ArgumentNullException(nameof(propertyService));
    }

    [HttpGet]
    public async Task<ActionResult<BaseEntityResponse<IReadOnlyCollection<PropertyDto>>>> GetProperties(
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
                    return BadRequest(new { error = $"Invalid property identifier: '{segment}'." });
                idList.Add(parsed);
            }
            parsedIds = idList;
        }

        var response = await _propertyService.ListAsync(tenantId, parsedIds, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        return Ok(response);
    }

    [HttpGet("{propertyId:guid}")]
    public async Task<ActionResult<PropertyResponse>> GetProperty(Guid tenantId, Guid propertyId, CancellationToken cancellationToken)
    {
        var response = await _propertyService.GetAsync(tenantId, propertyId, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }

    [HttpPost]
    public async Task<ActionResult<PropertyResponse>> CreateProperty(Guid tenantId, [FromBody] CreatePropertyRequest request, CancellationToken cancellationToken)
    {
        var response = await _propertyService.CreateAsync(tenantId, request, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        return CreatedAtAction(
            nameof(GetProperty),
            new { tenantId, propertyId = response.Entity?.Id },
            response);
    }

    [HttpPut("{propertyId:guid}")]
    public async Task<ActionResult<PropertyResponse>> UpdateProperty(Guid tenantId, Guid propertyId, [FromBody] UpdatePropertyRequest request, CancellationToken cancellationToken)
    {
        var response = await _propertyService.UpdateAsync(tenantId, propertyId, request, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }

    [HttpDelete("{propertyId:guid}")]
    public async Task<ActionResult<PropertyResponse>> DeleteProperty(Guid tenantId, Guid propertyId, CancellationToken cancellationToken)
    {
        var response = await _propertyService.DeleteAsync(tenantId, propertyId, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }
}
