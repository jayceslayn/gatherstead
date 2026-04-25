using Gatherstead.Api.Contracts.MealAttendance;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Security;
using Gatherstead.Api.Services.MealAttendance;
using Gatherstead.Api.Services.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gatherstead.Api.Controllers;

[ApiController]
[Authorize]
[RequireTenantAccess]
[Route("api/tenants/{tenantId:guid}/events/{eventId:guid}/meal-templates/{templateId:guid}/plans/{planId:guid}/attendance")]
public class MealAttendanceController : ControllerBase
{
    private readonly IMealAttendanceService _mealAttendanceService;

    public MealAttendanceController(IMealAttendanceService mealAttendanceService)
    {
        _mealAttendanceService = mealAttendanceService ?? throw new ArgumentNullException(nameof(mealAttendanceService));
    }

    [HttpGet]
    public async Task<ActionResult<BaseEntityResponse<IReadOnlyCollection<MealAttendanceDto>>>> GetMealAttendances(
        Guid tenantId,
        Guid planId,
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

        var response = await _mealAttendanceService.ListAsync(tenantId, planId, parsedMemberIds, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        return Ok(response);
    }

    [HttpGet("{attendanceId:guid}")]
    public async Task<ActionResult<MealAttendanceResponse>> GetMealAttendance(
        Guid tenantId,
        Guid planId,
        Guid attendanceId,
        CancellationToken cancellationToken)
    {
        var response = await _mealAttendanceService.GetAsync(tenantId, planId, attendanceId, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }

    [HttpPut]
    public async Task<ActionResult<MealAttendanceResponse>> UpsertMealAttendance(
        Guid tenantId,
        Guid planId,
        [FromQuery] Guid householdId,
        [FromBody] UpsertMealAttendanceRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _mealAttendanceService.UpsertAsync(tenantId, planId, householdId, request, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        return Ok(response);
    }

    [HttpDelete("{attendanceId:guid}")]
    public async Task<ActionResult<MealAttendanceResponse>> DeleteMealAttendance(
        Guid tenantId,
        Guid planId,
        Guid attendanceId,
        CancellationToken cancellationToken)
    {
        var response = await _mealAttendanceService.DeleteAsync(tenantId, planId, attendanceId, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }
}
