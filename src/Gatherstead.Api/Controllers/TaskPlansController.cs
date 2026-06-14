using Gatherstead.Api.Contracts.TaskPlans;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Security;
using Gatherstead.Api.Services.TaskPlans;
using Gatherstead.Api.Services.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gatherstead.Api.Controllers;

[ApiController]
[Authorize]
[RequireTenantAccess]
[Route("api/tenants/{tenantId:guid}/events/{eventId:guid}/task-templates/{templateId:guid}/plans")]
public class TaskPlansController : ControllerBase
{
    private readonly ITaskPlanService _taskPlanService;

    public TaskPlansController(ITaskPlanService taskPlanService)
    {
        _taskPlanService = taskPlanService ?? throw new ArgumentNullException(nameof(taskPlanService));
    }

    [HttpGet]
    public async Task<ActionResult<BaseEntityResponse<IReadOnlyCollection<TaskPlanDto>>>> GetTaskPlans(
        Guid tenantId,
        Guid eventId,
        Guid templateId,
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
                    return BadRequest(new { error = $"Invalid task plan identifier: '{segment}'." });
                idList.Add(parsed);
            }
            parsedIds = idList;
        }

        var response = await _taskPlanService.ListAsync(tenantId, eventId, templateId, parsedIds, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        return Ok(response);
    }

    [HttpGet("{planId:guid}")]
    public async Task<ActionResult<TaskPlanResponse>> GetTaskPlan(
        Guid tenantId,
        Guid templateId,
        Guid planId,
        CancellationToken cancellationToken)
    {
        var response = await _taskPlanService.GetAsync(tenantId, templateId, planId, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }

    [HttpPut("{planId:guid}")]
    public async Task<ActionResult<TaskPlanResponse>> UpdateTaskPlan(
        Guid tenantId,
        Guid templateId,
        Guid planId,
        [FromBody] UpdateTaskPlanRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _taskPlanService.UpdateAsync(tenantId, templateId, planId, request, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }

    [HttpDelete("{planId:guid}")]
    public async Task<ActionResult<TaskPlanResponse>> DeleteTaskPlan(
        Guid tenantId,
        Guid templateId,
        Guid planId,
        CancellationToken cancellationToken)
    {
        var response = await _taskPlanService.DeleteAsync(tenantId, templateId, planId, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }
}
