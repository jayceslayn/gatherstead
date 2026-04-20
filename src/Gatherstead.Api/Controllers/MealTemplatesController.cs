using Gatherstead.Api.Contracts.MealTemplates;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Security;
using Gatherstead.Api.Services.MealTemplates;
using Gatherstead.Api.Services.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gatherstead.Api.Controllers;

[ApiController]
[Authorize]
[RequireTenantAccess]
[Route("api/tenants/{tenantId:guid}/events/{eventId:guid}/meal-templates")]
public class MealTemplatesController : ControllerBase
{
    private readonly IMealTemplateService _mealTemplateService;

    public MealTemplatesController(IMealTemplateService mealTemplateService)
    {
        _mealTemplateService = mealTemplateService ?? throw new ArgumentNullException(nameof(mealTemplateService));
    }

    [HttpGet]
    public async Task<ActionResult<BaseEntityResponse<IReadOnlyCollection<MealTemplateDto>>>> GetMealTemplates(
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
                    return BadRequest(new { error = $"Invalid meal template identifier: '{segment}'." });
                idList.Add(parsed);
            }
            parsedIds = idList;
        }

        var response = await _mealTemplateService.ListAsync(tenantId, eventId, parsedIds, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        return Ok(response);
    }

    [HttpGet("{templateId:guid}")]
    public async Task<ActionResult<MealTemplateResponse>> GetMealTemplate(
        Guid tenantId,
        Guid eventId,
        Guid templateId,
        CancellationToken cancellationToken)
    {
        var response = await _mealTemplateService.GetAsync(tenantId, eventId, templateId, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }

    [HttpPost]
    public async Task<ActionResult<MealTemplateResponse>> CreateMealTemplate(
        Guid tenantId,
        Guid eventId,
        [FromBody] CreateMealTemplateRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _mealTemplateService.CreateAsync(tenantId, eventId, request, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        return CreatedAtAction(
            nameof(GetMealTemplate),
            new { tenantId, eventId, templateId = response.Entity?.Id },
            response);
    }

    [HttpPut("{templateId:guid}")]
    public async Task<ActionResult<MealTemplateResponse>> UpdateMealTemplate(
        Guid tenantId,
        Guid eventId,
        Guid templateId,
        [FromBody] UpdateMealTemplateRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _mealTemplateService.UpdateAsync(tenantId, eventId, templateId, request, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }

    [HttpDelete("{templateId:guid}")]
    public async Task<ActionResult<MealTemplateResponse>> DeleteMealTemplate(
        Guid tenantId,
        Guid eventId,
        Guid templateId,
        CancellationToken cancellationToken)
    {
        var response = await _mealTemplateService.DeleteAsync(tenantId, eventId, templateId, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }
}
