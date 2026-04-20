using Gatherstead.Api.Contracts.Events;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Security;
using Gatherstead.Api.Services.Events;
using Gatherstead.Api.Services.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gatherstead.Api.Controllers;

[ApiController]
[Authorize]
[RequireTenantAccess]
[Route("api/tenants/{tenantId:guid}/events")]
public class EventsController : ControllerBase
{
    private readonly IEventService _eventService;

    public EventsController(IEventService eventService)
    {
        _eventService = eventService ?? throw new ArgumentNullException(nameof(eventService));
    }

    [HttpGet]
    public async Task<ActionResult<BaseEntityResponse<IReadOnlyCollection<EventDto>>>> GetEvents(
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
                    return BadRequest(new { error = $"Invalid event identifier: '{segment}'." });
                idList.Add(parsed);
            }
            parsedIds = idList;
        }

        var response = await _eventService.ListAsync(tenantId, parsedIds, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        return Ok(response);
    }

    [HttpGet("{eventId:guid}")]
    public async Task<ActionResult<EventResponse>> GetEvent(Guid tenantId, Guid eventId, CancellationToken cancellationToken)
    {
        var response = await _eventService.GetAsync(tenantId, eventId, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }

    [HttpPost]
    public async Task<ActionResult<EventResponse>> CreateEvent(Guid tenantId, [FromBody] CreateEventRequest request, CancellationToken cancellationToken)
    {
        var response = await _eventService.CreateAsync(tenantId, request, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        return CreatedAtAction(
            nameof(GetEvent),
            new { tenantId, eventId = response.Entity?.Id },
            response);
    }

    [HttpPut("{eventId:guid}")]
    public async Task<ActionResult<EventResponse>> UpdateEvent(Guid tenantId, Guid eventId, [FromBody] UpdateEventRequest request, CancellationToken cancellationToken)
    {
        var response = await _eventService.UpdateAsync(tenantId, eventId, request, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }

    [HttpDelete("{eventId:guid}")]
    public async Task<ActionResult<EventResponse>> DeleteEvent(Guid tenantId, Guid eventId, CancellationToken cancellationToken)
    {
        var response = await _eventService.DeleteAsync(tenantId, eventId, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }

    [HttpPost("{eventId:guid}/sync-plans")]
    public async Task<ActionResult<EventResponse>> SyncPlans(Guid tenantId, Guid eventId, CancellationToken cancellationToken)
    {
        var response = await _eventService.SyncPlansAsync(tenantId, eventId, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }
}
