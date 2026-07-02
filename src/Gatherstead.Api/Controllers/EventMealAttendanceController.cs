using Gatherstead.Api.Contracts.MealAttendance;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Security;
using Gatherstead.Api.Services.MealAttendance;
using Gatherstead.Api.Services.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gatherstead.Api.Controllers;

/// <summary>Event-scoped meal attendance: a single read across every meal plan of the event and a
/// bulk upsert, so the sign-up UI avoids one request per plan/member.</summary>
[ApiController]
[Authorize]
[RequireTenantAccess]
[Route("api/tenants/{tenantId:guid}/events/{eventId:guid}/meal-attendance")]
public class EventMealAttendanceController : ControllerBase
{
    private readonly IMealAttendanceService _mealAttendanceService;

    public EventMealAttendanceController(IMealAttendanceService mealAttendanceService)
    {
        _mealAttendanceService = mealAttendanceService ?? throw new ArgumentNullException(nameof(mealAttendanceService));
    }

    [HttpGet]
    public async Task<ActionResult<BaseEntityResponse<IReadOnlyCollection<MealAttendanceDto>>>> GetForEvent(
        Guid tenantId,
        Guid eventId,
        [FromQuery] string? memberIds,
        CancellationToken cancellationToken)
    {
        if (!ControllerQueryParsing.TryParseGuidCsv(memberIds, out var parsedMemberIds, out var error))
            return BadRequest(new { error });

        var response = await _mealAttendanceService.ListForEventAsync(tenantId, eventId, parsedMemberIds, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        return Ok(response);
    }

    [HttpPut("bulk")]
    public async Task<ActionResult<BulkUpsertResponse<MealAttendanceDto>>> BulkUpsert(
        Guid tenantId,
        Guid eventId,
        [FromBody] BulkUpsertMealAttendanceRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _mealAttendanceService.BulkUpsertAsync(tenantId, eventId, request, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        return Ok(response);
    }
}
