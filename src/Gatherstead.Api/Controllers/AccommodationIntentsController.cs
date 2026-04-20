using Gatherstead.Api.Contracts.AccommodationIntents;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Security;
using Gatherstead.Api.Services.AccommodationIntents;
using Gatherstead.Api.Services.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gatherstead.Api.Controllers;

[ApiController]
[Authorize]
[RequireTenantAccess]
[Route("api/tenants/{tenantId:guid}/properties/{propertyId:guid}/accommodations/{accommodationId:guid}/intents")]
public class AccommodationIntentsController : ControllerBase
{
    private readonly IAccommodationIntentService _accommodationIntentService;

    public AccommodationIntentsController(IAccommodationIntentService accommodationIntentService)
    {
        _accommodationIntentService = accommodationIntentService ?? throw new ArgumentNullException(nameof(accommodationIntentService));
    }

    [HttpGet]
    public async Task<ActionResult<BaseEntityResponse<IReadOnlyCollection<AccommodationIntentDto>>>> GetAccommodationIntents(
        Guid tenantId,
        Guid accommodationId,
        [FromQuery] string? memberIds,
        CancellationToken cancellationToken)
    {
        IEnumerable<Guid>? parsedMemberIds = null;
        if (!string.IsNullOrWhiteSpace(memberIds))
        {
            var idList = new List<Guid>();
            foreach (var segment in memberIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!Guid.TryParse(segment, out var parsed))
                    return BadRequest(new { error = $"Invalid member identifier: '{segment}'." });
                idList.Add(parsed);
            }
            parsedMemberIds = idList;
        }

        var response = await _accommodationIntentService.ListAsync(tenantId, accommodationId, parsedMemberIds, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        return Ok(response);
    }

    [HttpGet("{intentId:guid}")]
    public async Task<ActionResult<AccommodationIntentResponse>> GetAccommodationIntent(
        Guid tenantId,
        Guid accommodationId,
        Guid intentId,
        CancellationToken cancellationToken)
    {
        var response = await _accommodationIntentService.GetAsync(tenantId, accommodationId, intentId, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }

    [HttpPost]
    public async Task<ActionResult<AccommodationIntentResponse>> CreateAccommodationIntent(
        Guid tenantId,
        Guid accommodationId,
        [FromQuery] Guid householdId,
        [FromBody] CreateAccommodationIntentRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _accommodationIntentService.CreateAsync(tenantId, accommodationId, householdId, request, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        return CreatedAtAction(
            nameof(GetAccommodationIntent),
            new { tenantId, propertyId = (Guid?)null, accommodationId, intentId = response.Entity?.Id },
            response);
    }

    [HttpPut("{intentId:guid}")]
    public async Task<ActionResult<AccommodationIntentResponse>> UpdateAccommodationIntent(
        Guid tenantId,
        Guid accommodationId,
        Guid intentId,
        [FromBody] UpdateAccommodationIntentRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _accommodationIntentService.UpdateAsync(tenantId, accommodationId, intentId, request, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }

    [HttpDelete("{intentId:guid}")]
    public async Task<ActionResult<AccommodationIntentResponse>> DeleteAccommodationIntent(
        Guid tenantId,
        Guid accommodationId,
        Guid intentId,
        CancellationToken cancellationToken)
    {
        var response = await _accommodationIntentService.DeleteAsync(tenantId, accommodationId, intentId, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }
}
