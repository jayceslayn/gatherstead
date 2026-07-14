using Gatherstead.Api.Contracts.TaskIntents;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Security;
using Gatherstead.Api.Services.TaskIntents;
using Gatherstead.Api.Services.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gatherstead.Api.Controllers;

[ApiController]
[Authorize]
[RequireTenantAccess]
[Route("api/tenants/{tenantId:guid}/events/{eventId:guid}/task-templates/{templateId:guid}/plans/{planId:guid}/intents")]
public class TaskIntentsController : ControllerBase
{
    private readonly ITaskIntentService _taskIntentService;

    public TaskIntentsController(ITaskIntentService taskIntentService)
    {
        _taskIntentService = taskIntentService ?? throw new ArgumentNullException(nameof(taskIntentService));
    }

    [HttpGet]
    public async Task<ActionResult<BaseEntityResponse<IReadOnlyCollection<TaskIntentDto>>>> GetTaskIntents(
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

        var response = await _taskIntentService.ListAsync(tenantId, planId, parsedMemberIds, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return this.ToErrorResult(response);

        return Ok(response);
    }

    [HttpGet("{intentId:guid}")]
    public async Task<ActionResult<TaskIntentResponse>> GetTaskIntent(
        Guid tenantId,
        Guid planId,
        Guid intentId,
        CancellationToken cancellationToken)
    {
        var response = await _taskIntentService.GetAsync(tenantId, planId, intentId, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return this.ToErrorResult(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }

    [HttpPut]
    public async Task<ActionResult<TaskIntentResponse>> UpsertTaskIntent(
        Guid tenantId,
        Guid planId,
        [FromQuery] Guid householdId,
        [FromBody] UpsertTaskIntentRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _taskIntentService.UpsertAsync(tenantId, planId, householdId, request, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return this.ToErrorResult(response);

        return Ok(response);
    }

    [HttpDelete("{intentId:guid}")]
    public async Task<ActionResult<TaskIntentResponse>> DeleteTaskIntent(
        Guid tenantId,
        Guid planId,
        Guid intentId,
        CancellationToken cancellationToken)
    {
        var response = await _taskIntentService.DeleteAsync(tenantId, planId, intentId, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return this.ToErrorResult(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }
}
