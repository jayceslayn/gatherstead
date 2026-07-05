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
[Route("api/tenants/{tenantId:guid}/accommodations")]
public class AccommodationSearchController : ControllerBase
{
    private readonly IAccommodationAvailabilityService _availabilityService;

    public AccommodationSearchController(IAccommodationAvailabilityService availabilityService)
    {
        _availabilityService = availabilityService ?? throw new ArgumentNullException(nameof(availabilityService));
    }

    [HttpGet("availability")]
    public async Task<ActionResult<BaseEntityResponse<IReadOnlyCollection<AccommodationAvailabilityDto>>>> GetAvailability(
        Guid tenantId,
        [FromQuery] DateOnly startNight,
        [FromQuery] DateOnly endNight,
        [FromQuery] int? partyAdults,
        [FromQuery] int? partyChildren,
        [FromQuery] bool? requireCapacity,
        [FromQuery] Guid[]? propertyIds,
        CancellationToken cancellationToken)
    {
        var response = await _availabilityService.SearchAsync(
            tenantId, startNight, endNight, partyAdults, partyChildren, requireCapacity ?? true, propertyIds, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        return Ok(response);
    }
}
