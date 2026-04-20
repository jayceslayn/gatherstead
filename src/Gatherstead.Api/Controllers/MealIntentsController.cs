using Gatherstead.Api.Contracts.MealIntents;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Security;
using Gatherstead.Api.Services.MealIntents;
using Gatherstead.Api.Services.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gatherstead.Api.Controllers;

[ApiController]
[Authorize]
[RequireTenantAccess]
[Route("api/tenants/{tenantId:guid}/events/{eventId:guid}/meal-templates/{templateId:guid}/plans/{planId:guid}/intents")]
public class MealIntentsController : ControllerBase
{
    private readonly IMealIntentService _mealIntentService;

    public MealIntentsController(IMealIntentService mealIntentService)
    {
        _mealIntentService = mealIntentService ?? throw new ArgumentNullException(nameof(mealIntentService));
    }

    [HttpGet]
    public async Task<ActionResult<BaseEntityResponse<IReadOnlyCollection<MealIntentDto>>>> GetMealIntents(
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

        var response = await _mealIntentService.ListAsync(tenantId, planId, parsedMemberIds, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        return Ok(response);
    }

    [HttpGet("{intentId:guid}")]
    public async Task<ActionResult<MealIntentResponse>> GetMealIntent(
        Guid tenantId,
        Guid planId,
        Guid intentId,
        CancellationToken cancellationToken)
    {
        var response = await _mealIntentService.GetAsync(tenantId, planId, intentId, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }

    [HttpPut]
    public async Task<ActionResult<MealIntentResponse>> UpsertMealIntent(
        Guid tenantId,
        Guid planId,
        [FromQuery] Guid householdId,
        [FromBody] UpsertMealIntentRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _mealIntentService.UpsertAsync(tenantId, planId, householdId, request, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        return Ok(response);
    }

    [HttpDelete("{intentId:guid}")]
    public async Task<ActionResult<MealIntentResponse>> DeleteMealIntent(
        Guid tenantId,
        Guid planId,
        Guid intentId,
        CancellationToken cancellationToken)
    {
        var response = await _mealIntentService.DeleteAsync(tenantId, planId, intentId, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }
}
