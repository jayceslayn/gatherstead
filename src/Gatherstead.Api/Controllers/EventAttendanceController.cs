using Gatherstead.Api.Contracts.EventAttendance;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Security;
using Gatherstead.Api.Services.EventAttendance;
using Gatherstead.Api.Services.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gatherstead.Api.Controllers;

[ApiController]
[Authorize]
[RequireTenantAccess]
[Route("api/tenants/{tenantId:guid}/events/{eventId:guid}/attendance")]
public class EventAttendanceController : ControllerBase
{
    private readonly IEventAttendanceService _eventAttendanceService;

    public EventAttendanceController(IEventAttendanceService eventAttendanceService)
    {
        _eventAttendanceService = eventAttendanceService ?? throw new ArgumentNullException(nameof(eventAttendanceService));
    }

    [HttpGet]
    public async Task<ActionResult<BaseEntityResponse<IReadOnlyCollection<EventAttendanceDto>>>> GetAttendances(
        Guid tenantId,
        Guid eventId,
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

        var response = await _eventAttendanceService.ListAsync(tenantId, eventId, parsedMemberIds, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        return Ok(response);
    }

    [HttpGet("{attendanceId:guid}")]
    public async Task<ActionResult<EventAttendanceResponse>> GetAttendance(
        Guid tenantId,
        Guid eventId,
        Guid attendanceId,
        CancellationToken cancellationToken)
    {
        var response = await _eventAttendanceService.GetAsync(tenantId, eventId, attendanceId, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }

    [HttpPut]
    public async Task<ActionResult<EventAttendanceResponse>> UpsertAttendance(
        Guid tenantId,
        Guid eventId,
        [FromQuery] Guid householdId,
        [FromBody] UpsertEventAttendanceRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _eventAttendanceService.UpsertAsync(tenantId, eventId, householdId, request, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        return Ok(response);
    }

    [HttpDelete("{attendanceId:guid}")]
    public async Task<ActionResult<EventAttendanceResponse>> DeleteAttendance(
        Guid tenantId,
        Guid eventId,
        Guid attendanceId,
        CancellationToken cancellationToken)
    {
        var response = await _eventAttendanceService.DeleteAsync(tenantId, eventId, attendanceId, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }
}
