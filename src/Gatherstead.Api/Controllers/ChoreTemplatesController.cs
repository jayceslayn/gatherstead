using Gatherstead.Api.Contracts.ChoreTemplates;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Security;
using Gatherstead.Api.Services.ChoreTemplates;
using Gatherstead.Api.Services.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gatherstead.Api.Controllers;

[ApiController]
[Authorize]
[RequireTenantAccess]
[Route("api/tenants/{tenantId:guid}/events/{eventId:guid}/chore-templates")]
public class ChoreTemplatesController : ControllerBase
{
    private readonly IChoreTemplateService _choreTemplateService;

    public ChoreTemplatesController(IChoreTemplateService choreTemplateService)
    {
        _choreTemplateService = choreTemplateService ?? throw new ArgumentNullException(nameof(choreTemplateService));
    }

    [HttpGet]
    public async Task<ActionResult<BaseEntityResponse<IReadOnlyCollection<ChoreTemplateDto>>>> GetChoreTemplates(
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
                    return BadRequest(new { error = $"Invalid chore template identifier: '{segment}'." });
                idList.Add(parsed);
            }
            parsedIds = idList;
        }

        var response = await _choreTemplateService.ListAsync(tenantId, eventId, parsedIds, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        return Ok(response);
    }

    [HttpGet("{templateId:guid}")]
    public async Task<ActionResult<ChoreTemplateResponse>> GetChoreTemplate(
        Guid tenantId,
        Guid eventId,
        Guid templateId,
        CancellationToken cancellationToken)
    {
        var response = await _choreTemplateService.GetAsync(tenantId, eventId, templateId, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }

    [HttpPost]
    public async Task<ActionResult<ChoreTemplateResponse>> CreateChoreTemplate(
        Guid tenantId,
        Guid eventId,
        [FromBody] CreateChoreTemplateRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _choreTemplateService.CreateAsync(tenantId, eventId, request, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        return CreatedAtAction(
            nameof(GetChoreTemplate),
            new { tenantId, eventId, templateId = response.Entity?.Id },
            response);
    }

    [HttpPut("{templateId:guid}")]
    public async Task<ActionResult<ChoreTemplateResponse>> UpdateChoreTemplate(
        Guid tenantId,
        Guid eventId,
        Guid templateId,
        [FromBody] UpdateChoreTemplateRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _choreTemplateService.UpdateAsync(tenantId, eventId, templateId, request, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }

    [HttpDelete("{templateId:guid}")]
    public async Task<ActionResult<ChoreTemplateResponse>> DeleteChoreTemplate(
        Guid tenantId,
        Guid eventId,
        Guid templateId,
        CancellationToken cancellationToken)
    {
        var response = await _choreTemplateService.DeleteAsync(tenantId, eventId, templateId, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }
}
