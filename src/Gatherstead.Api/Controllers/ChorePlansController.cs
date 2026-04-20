using Gatherstead.Api.Contracts.ChorePlans;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Security;
using Gatherstead.Api.Services.ChorePlans;
using Gatherstead.Api.Services.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gatherstead.Api.Controllers;

[ApiController]
[Authorize]
[RequireTenantAccess]
[Route("api/tenants/{tenantId:guid}/events/{eventId:guid}/chore-templates/{templateId:guid}/plans")]
public class ChorePlansController : ControllerBase
{
    private readonly IChorePlanService _chorePlanService;

    public ChorePlansController(IChorePlanService chorePlanService)
    {
        _chorePlanService = chorePlanService ?? throw new ArgumentNullException(nameof(chorePlanService));
    }

    [HttpGet]
    public async Task<ActionResult<BaseEntityResponse<IReadOnlyCollection<ChorePlanDto>>>> GetChorePlans(
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
                    return BadRequest(new { error = $"Invalid chore plan identifier: '{segment}'." });
                idList.Add(parsed);
            }
            parsedIds = idList;
        }

        var response = await _chorePlanService.ListAsync(tenantId, eventId, templateId, parsedIds, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        return Ok(response);
    }

    [HttpGet("{planId:guid}")]
    public async Task<ActionResult<ChorePlanResponse>> GetChorePlan(
        Guid tenantId,
        Guid templateId,
        Guid planId,
        CancellationToken cancellationToken)
    {
        var response = await _chorePlanService.GetAsync(tenantId, templateId, planId, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }

    [HttpPut("{planId:guid}")]
    public async Task<ActionResult<ChorePlanResponse>> UpdateChorePlan(
        Guid tenantId,
        Guid templateId,
        Guid planId,
        [FromBody] UpdateChorePlanRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _chorePlanService.UpdateAsync(tenantId, templateId, planId, request, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }
}
