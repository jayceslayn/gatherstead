using Gatherstead.Api.Contracts.TaskTemplateAttributes;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Security;
using Gatherstead.Api.Services.TaskTemplateAttributes;
using Gatherstead.Api.Services.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gatherstead.Api.Controllers;

[ApiController]
[Authorize]
[RequireTenantAccess]
[Route("api/tenants/{tenantId:guid}/task-templates/{taskTemplateId:guid}/attributes")]
public class TaskTemplateAttributesController : ControllerBase
{
    private readonly ITaskTemplateAttributeService _attributeService;

    public TaskTemplateAttributesController(ITaskTemplateAttributeService attributeService)
    {
        _attributeService = attributeService ?? throw new ArgumentNullException(nameof(attributeService));
    }

    [HttpGet]
    public async Task<ActionResult<BaseEntityResponse<IReadOnlyCollection<TaskTemplateAttributeDto>>>> GetAttributes(
        Guid tenantId,
        Guid taskTemplateId,
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
                    return BadRequest(new { error = $"Invalid attribute identifier: '{segment}'." });
                idList.Add(parsed);
            }
            parsedIds = idList;
        }

        var response = await _attributeService.ListAsync(tenantId, taskTemplateId, parsedIds, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        return Ok(response);
    }

    [HttpGet("{attributeId:guid}")]
    public async Task<ActionResult<TaskTemplateAttributeResponse>> GetAttribute(Guid tenantId, Guid taskTemplateId, Guid attributeId, CancellationToken cancellationToken)
    {
        var response = await _attributeService.GetAsync(tenantId, taskTemplateId, attributeId, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }

    [HttpPost]
    public async Task<ActionResult<TaskTemplateAttributeResponse>> CreateAttribute(Guid tenantId, Guid taskTemplateId, [FromBody] CreateTaskTemplateAttributeRequest request, CancellationToken cancellationToken)
    {
        var response = await _attributeService.CreateAsync(tenantId, taskTemplateId, request, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        return CreatedAtAction(
            nameof(GetAttribute),
            new { tenantId, taskTemplateId, attributeId = response.Entity?.Id },
            response);
    }

    [HttpPut("{attributeId:guid}")]
    public async Task<ActionResult<TaskTemplateAttributeResponse>> UpdateAttribute(Guid tenantId, Guid taskTemplateId, Guid attributeId, [FromBody] UpdateTaskTemplateAttributeRequest request, CancellationToken cancellationToken)
    {
        var response = await _attributeService.UpdateAsync(tenantId, taskTemplateId, attributeId, request, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }

    [HttpDelete("{attributeId:guid}")]
    public async Task<ActionResult<TaskTemplateAttributeResponse>> DeleteAttribute(Guid tenantId, Guid taskTemplateId, Guid attributeId, CancellationToken cancellationToken)
    {
        var response = await _attributeService.DeleteAsync(tenantId, taskTemplateId, attributeId, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }
}
