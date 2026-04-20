using Gatherstead.Api.Contracts.MealPlans;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Security;
using Gatherstead.Api.Services.MealPlans;
using Gatherstead.Api.Services.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gatherstead.Api.Controllers;

[ApiController]
[Authorize]
[RequireTenantAccess]
[Route("api/tenants/{tenantId:guid}/events/{eventId:guid}/meal-templates/{templateId:guid}/plans")]
public class MealPlansController : ControllerBase
{
    private readonly IMealPlanService _mealPlanService;

    public MealPlansController(IMealPlanService mealPlanService)
    {
        _mealPlanService = mealPlanService ?? throw new ArgumentNullException(nameof(mealPlanService));
    }

    [HttpGet]
    public async Task<ActionResult<BaseEntityResponse<IReadOnlyCollection<MealPlanDto>>>> GetMealPlans(
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
                    return BadRequest(new { error = $"Invalid meal plan identifier: '{segment}'." });
                idList.Add(parsed);
            }
            parsedIds = idList;
        }

        var response = await _mealPlanService.ListAsync(tenantId, eventId, templateId, parsedIds, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        return Ok(response);
    }

    [HttpGet("{planId:guid}")]
    public async Task<ActionResult<MealPlanResponse>> GetMealPlan(
        Guid tenantId,
        Guid templateId,
        Guid planId,
        CancellationToken cancellationToken)
    {
        var response = await _mealPlanService.GetAsync(tenantId, templateId, planId, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }

    [HttpPut("{planId:guid}")]
    public async Task<ActionResult<MealPlanResponse>> UpdateMealPlan(
        Guid tenantId,
        Guid templateId,
        Guid planId,
        [FromBody] UpdateMealPlanRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _mealPlanService.UpdateAsync(tenantId, templateId, planId, request, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }
}
