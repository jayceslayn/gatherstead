using Gatherstead.Api.Contracts.TaskIntents;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Security;
using Gatherstead.Api.Services.TaskIntents;
using Gatherstead.Api.Services.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gatherstead.Api.Controllers;

/// <summary>Tenant-wide read of volunteered task intents across every event, for the
/// "My Upcoming Tasks" dashboard widget and tasks feature page.</summary>
[ApiController]
[Authorize]
[RequireTenantAccess]
[Route("api/tenants/{tenantId:guid}/task-intents")]
public class TaskIntentsQueryController : ControllerBase
{
    private readonly ITaskIntentService _taskIntentService;

    public TaskIntentsQueryController(ITaskIntentService taskIntentService)
    {
        _taskIntentService = taskIntentService ?? throw new ArgumentNullException(nameof(taskIntentService));
    }

    [HttpGet]
    public async Task<ActionResult<BaseEntityResponse<IReadOnlyCollection<MyTaskDto>>>> GetTasks(
        Guid tenantId,
        [FromQuery] string? memberIds,
        [FromQuery] DateOnly? fromDay,
        CancellationToken cancellationToken)
    {
        if (!ControllerQueryParsing.TryParseGuidCsv(memberIds, out var parsedMemberIds, out var error))
            return BadRequest(new { error });

        var response = await _taskIntentService.ListForMemberAsync(tenantId, parsedMemberIds, fromDay, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return this.ToErrorResult(response);

        return Ok(response);
    }
}
