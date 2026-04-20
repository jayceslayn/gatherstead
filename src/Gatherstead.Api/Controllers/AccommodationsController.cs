using Gatherstead.Api.Contracts.Accommodations;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Security;
using Gatherstead.Api.Services.Accommodations;
using Gatherstead.Api.Services.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gatherstead.Api.Controllers;

[ApiController]
[Authorize]
[RequireTenantAccess]
[Route("api/tenants/{tenantId:guid}/properties/{propertyId:guid}/accommodations")]
public class AccommodationsController : ControllerBase
{
    private readonly IAccommodationService _accommodationService;

    public AccommodationsController(IAccommodationService accommodationService)
    {
        _accommodationService = accommodationService ?? throw new ArgumentNullException(nameof(accommodationService));
    }

    [HttpGet]
    public async Task<ActionResult<BaseEntityResponse<IReadOnlyCollection<AccommodationDto>>>> GetAccommodations(
        Guid tenantId,
        Guid propertyId,
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
                    return BadRequest(new { error = $"Invalid accommodation identifier: '{segment}'." });
                idList.Add(parsed);
            }
            parsedIds = idList;
        }

        var response = await _accommodationService.ListAsync(tenantId, propertyId, parsedIds, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        return Ok(response);
    }

    [HttpGet("{accommodationId:guid}")]
    public async Task<ActionResult<AccommodationResponse>> GetAccommodation(
        Guid tenantId,
        Guid propertyId,
        Guid accommodationId,
        CancellationToken cancellationToken)
    {
        var response = await _accommodationService.GetAsync(tenantId, propertyId, accommodationId, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }

    [HttpPost]
    public async Task<ActionResult<AccommodationResponse>> CreateAccommodation(
        Guid tenantId,
        Guid propertyId,
        [FromBody] CreateAccommodationRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _accommodationService.CreateAsync(tenantId, propertyId, request, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        return CreatedAtAction(
            nameof(GetAccommodation),
            new { tenantId, propertyId, accommodationId = response.Entity?.Id },
            response);
    }

    [HttpPut("{accommodationId:guid}")]
    public async Task<ActionResult<AccommodationResponse>> UpdateAccommodation(
        Guid tenantId,
        Guid propertyId,
        Guid accommodationId,
        [FromBody] UpdateAccommodationRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _accommodationService.UpdateAsync(tenantId, propertyId, accommodationId, request, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }

    [HttpDelete("{accommodationId:guid}")]
    public async Task<ActionResult<AccommodationResponse>> DeleteAccommodation(
        Guid tenantId,
        Guid propertyId,
        Guid accommodationId,
        CancellationToken cancellationToken)
    {
        var response = await _accommodationService.DeleteAsync(tenantId, propertyId, accommodationId, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }
}
