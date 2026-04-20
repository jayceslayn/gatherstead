using Gatherstead.Api.Contracts.ChoreIntents;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Security;
using Gatherstead.Api.Services.ChoreIntents;
using Gatherstead.Api.Services.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gatherstead.Api.Controllers;

[ApiController]
[Authorize]
[RequireTenantAccess]
[Route("api/tenants/{tenantId:guid}/events/{eventId:guid}/chore-templates/{templateId:guid}/plans/{planId:guid}/intents")]
public class ChoreIntentsController : ControllerBase
{
    private readonly IChoreIntentService _choreIntentService;

    public ChoreIntentsController(IChoreIntentService choreIntentService)
    {
        _choreIntentService = choreIntentService ?? throw new ArgumentNullException(nameof(choreIntentService));
    }

    [HttpGet]
    public async Task<ActionResult<BaseEntityResponse<IReadOnlyCollection<ChoreIntentDto>>>> GetChoreIntents(
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

        var response = await _choreIntentService.ListAsync(tenantId, planId, parsedMemberIds, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        return Ok(response);
    }

    [HttpGet("{intentId:guid}")]
    public async Task<ActionResult<ChoreIntentResponse>> GetChoreIntent(
        Guid tenantId,
        Guid planId,
        Guid intentId,
        CancellationToken cancellationToken)
    {
        var response = await _choreIntentService.GetAsync(tenantId, planId, intentId, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }

    [HttpPut]
    public async Task<ActionResult<ChoreIntentResponse>> UpsertChoreIntent(
        Guid tenantId,
        Guid planId,
        [FromQuery] Guid householdId,
        [FromBody] UpsertChoreIntentRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _choreIntentService.UpsertAsync(tenantId, planId, householdId, request, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        return Ok(response);
    }

    [HttpDelete("{intentId:guid}")]
    public async Task<ActionResult<ChoreIntentResponse>> DeleteChoreIntent(
        Guid tenantId,
        Guid planId,
        Guid intentId,
        CancellationToken cancellationToken)
    {
        var response = await _choreIntentService.DeleteAsync(tenantId, planId, intentId, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }
}
