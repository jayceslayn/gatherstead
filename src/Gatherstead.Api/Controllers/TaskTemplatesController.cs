using Gatherstead.Api.Contracts.TaskTemplates;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Security;
using Gatherstead.Api.Services.TaskTemplates;
using Gatherstead.Api.Services.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gatherstead.Api.Controllers;

[ApiController]
[Authorize]
[RequireTenantAccess]
[Route("api/tenants/{tenantId:guid}/events/{eventId:guid}/task-templates")]
public class TaskTemplatesController : ControllerBase
{
    private readonly ITaskTemplateService _taskTemplateService;

    public TaskTemplatesController(ITaskTemplateService taskTemplateService)
    {
        _taskTemplateService = taskTemplateService ?? throw new ArgumentNullException(nameof(taskTemplateService));
    }

    [HttpGet]
    public async Task<ActionResult<BaseEntityResponse<IReadOnlyCollection<TaskTemplateDto>>>> GetTaskTemplates(
        Guid tenantId,
        Guid eventId,
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
                    return BadRequest(new { error = $"Invalid task template identifier: '{segment}'." });
                idList.Add(parsed);
            }
            parsedIds = idList;
        }

        var response = await _taskTemplateService.ListAsync(tenantId, eventId, parsedIds, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return this.ToErrorResult(response);

        return Ok(response);
    }

    [HttpGet("{templateId:guid}")]
    public async Task<ActionResult<TaskTemplateResponse>> GetTaskTemplate(
        Guid tenantId,
        Guid eventId,
        Guid templateId,
        CancellationToken cancellationToken)
    {
        var response = await _taskTemplateService.GetAsync(tenantId, eventId, templateId, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return this.ToErrorResult(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }

    [HttpPost]
    public async Task<ActionResult<TaskTemplateResponse>> CreateTaskTemplate(
        Guid tenantId,
        Guid eventId,
        [FromBody] CreateTaskTemplateRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _taskTemplateService.CreateAsync(tenantId, eventId, request, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return this.ToErrorResult(response);

        return CreatedAtAction(
            nameof(GetTaskTemplate),
            new { tenantId, eventId, templateId = response.Entity?.Id },
            response);
    }

    [HttpPut("{templateId:guid}")]
    public async Task<ActionResult<TaskTemplateResponse>> UpdateTaskTemplate(
        Guid tenantId,
        Guid eventId,
        Guid templateId,
        [FromBody] UpdateTaskTemplateRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _taskTemplateService.UpdateAsync(tenantId, eventId, templateId, request, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return this.ToErrorResult(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }

    [HttpDelete("{templateId:guid}")]
    public async Task<ActionResult<TaskTemplateResponse>> DeleteTaskTemplate(
        Guid tenantId,
        Guid eventId,
        Guid templateId,
        CancellationToken cancellationToken)
    {
        var response = await _taskTemplateService.DeleteAsync(tenantId, eventId, templateId, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return this.ToErrorResult(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }
}
