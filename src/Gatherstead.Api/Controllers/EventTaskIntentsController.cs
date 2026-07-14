using Gatherstead.Api.Contracts.TaskIntents;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Security;
using Gatherstead.Api.Services.TaskIntents;
using Gatherstead.Api.Services.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gatherstead.Api.Controllers;

/// <summary>Event-scoped task intents: a single read across every task plan of the event and a bulk
/// upsert, so the sign-up UI avoids one request per plan/member.</summary>
[ApiController]
[Authorize]
[RequireTenantAccess]
[Route("api/tenants/{tenantId:guid}/events/{eventId:guid}/task-intents")]
public class EventTaskIntentsController : ControllerBase
{
    private readonly ITaskIntentService _taskIntentService;

    public EventTaskIntentsController(ITaskIntentService taskIntentService)
    {
        _taskIntentService = taskIntentService ?? throw new ArgumentNullException(nameof(taskIntentService));
    }

    [HttpGet]
    public async Task<ActionResult<BaseEntityResponse<IReadOnlyCollection<TaskIntentDto>>>> GetForEvent(
        Guid tenantId,
        Guid eventId,
        [FromQuery] string? memberIds,
        CancellationToken cancellationToken)
    {
        if (!ControllerQueryParsing.TryParseGuidCsv(memberIds, out var parsedMemberIds, out var error))
            return BadRequest(new { error });

        var response = await _taskIntentService.ListForEventAsync(tenantId, eventId, parsedMemberIds, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return this.ToErrorResult(response);

        return Ok(response);
    }

    [HttpPut("bulk")]
    public async Task<ActionResult<BulkUpsertResponse<TaskIntentDto>>> BulkUpsert(
        Guid tenantId,
        Guid eventId,
        [FromBody] BulkUpsertTaskIntentRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _taskIntentService.BulkUpsertAsync(tenantId, eventId, request, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return this.ToErrorResult(response);

        return Ok(response);
    }
}
