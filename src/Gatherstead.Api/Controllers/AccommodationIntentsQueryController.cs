using Gatherstead.Api.Contracts.AccommodationIntents;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Security;
using Gatherstead.Api.Services.AccommodationIntents;
using Gatherstead.Api.Services.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gatherstead.Api.Controllers;

/// <summary>Tenant-wide read of accommodation stays across every accommodation, for the top-level
/// Accommodations feature and the "My Upcoming Stays" dashboard widget.</summary>
[ApiController]
[Authorize]
[RequireTenantAccess]
[Route("api/tenants/{tenantId:guid}/accommodation-intents")]
public class AccommodationIntentsQueryController : ControllerBase
{
    private readonly IAccommodationIntentService _accommodationIntentService;

    public AccommodationIntentsQueryController(IAccommodationIntentService accommodationIntentService)
    {
        _accommodationIntentService = accommodationIntentService ?? throw new ArgumentNullException(nameof(accommodationIntentService));
    }

    [HttpGet]
    public async Task<ActionResult<BaseEntityResponse<IReadOnlyCollection<MyStayDto>>>> GetStays(
        Guid tenantId,
        [FromQuery] string? memberIds,
        [FromQuery] DateOnly? fromNight,
        CancellationToken cancellationToken)
    {
        if (!ControllerQueryParsing.TryParseGuidCsv(memberIds, out var parsedMemberIds, out var error))
            return BadRequest(new { error });

        var response = await _accommodationIntentService.ListForTenantAsync(tenantId, parsedMemberIds, fromNight, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return this.ToErrorResult(response);

        return Ok(response);
    }
}
