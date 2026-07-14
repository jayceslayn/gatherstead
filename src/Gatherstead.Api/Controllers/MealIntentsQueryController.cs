using Gatherstead.Api.Contracts.MealIntents;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Security;
using Gatherstead.Api.Services.MealIntents;
using Gatherstead.Api.Services.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gatherstead.Api.Controllers;

/// <summary>Tenant-wide read of volunteered cook sign-ups across every event, for the
/// "My Upcoming Meals" dashboard widget and meal planner edit gating.</summary>
[ApiController]
[Authorize]
[RequireTenantAccess]
[Route("api/tenants/{tenantId:guid}/meal-intents")]
public class MealIntentsQueryController : ControllerBase
{
    private readonly IMealIntentService _mealIntentService;

    public MealIntentsQueryController(IMealIntentService mealIntentService)
    {
        _mealIntentService = mealIntentService ?? throw new ArgumentNullException(nameof(mealIntentService));
    }

    [HttpGet]
    public async Task<ActionResult<BaseEntityResponse<IReadOnlyCollection<MyMealDto>>>> GetMeals(
        Guid tenantId,
        [FromQuery] string? memberIds,
        [FromQuery] DateOnly? fromDay,
        CancellationToken cancellationToken)
    {
        if (!ControllerQueryParsing.TryParseGuidCsv(memberIds, out var parsedMemberIds, out var error))
            return BadRequest(new { error });

        var response = await _mealIntentService.ListForMemberAsync(tenantId, parsedMemberIds, fromDay, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        return Ok(response);
    }
}
