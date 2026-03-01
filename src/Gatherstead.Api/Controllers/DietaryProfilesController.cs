using Gatherstead.Api.Contracts.DietaryProfiles;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Security;
using Gatherstead.Api.Services.DietaryProfiles;
using Gatherstead.Api.Services.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gatherstead.Api.Controllers;

[ApiController]
[Authorize]
[RequireTenantAccess]
[Route("api/tenants/{tenantId:guid}/households/{householdId:guid}/members/{memberId:guid}/dietary-profile")]
public class DietaryProfilesController : ControllerBase
{
    private readonly IDietaryProfileService _dietaryProfileService;

    public DietaryProfilesController(IDietaryProfileService dietaryProfileService)
    {
        _dietaryProfileService = dietaryProfileService ?? throw new ArgumentNullException(nameof(dietaryProfileService));
    }

    [HttpGet("~/api/tenants/{tenantId:guid}/dietary-profiles")]
    public async Task<ActionResult<BaseEntityResponse<IReadOnlyCollection<DietaryProfileDto>>>> GetDietaryProfiles(
        Guid tenantId,
        [FromQuery] string? memberIds,
        CancellationToken cancellationToken)
    {
        IEnumerable<Guid>? parsedIds = null;
        if (!string.IsNullOrWhiteSpace(memberIds))
        {
            var idList = new List<Guid>();
            foreach (var segment in memberIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!Guid.TryParse(segment, out var parsed))
                {
                    return BadRequest(new { error = $"Invalid member identifier: '{segment}'." });
                }
                idList.Add(parsed);
            }
            parsedIds = idList;
        }

        var response = await _dietaryProfileService.ListAsync(tenantId, parsedIds, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    [HttpGet]
    public async Task<ActionResult<DietaryProfileResponse>> GetDietaryProfile(Guid tenantId, Guid householdId, Guid memberId, CancellationToken cancellationToken)
    {
        var response = await _dietaryProfileService.GetAsync(tenantId, householdId, memberId, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
        {
            return BadRequest(response);
        }

        if (response.Entity is null)
        {
            return NotFound(response);
        }

        return Ok(response);
    }

    [HttpPut]
    public async Task<ActionResult<DietaryProfileResponse>> UpsertDietaryProfile(Guid tenantId, Guid householdId, Guid memberId, [FromBody] UpsertDietaryProfileRequest request, CancellationToken cancellationToken)
    {
        var response = await _dietaryProfileService.UpsertAsync(tenantId, householdId, memberId, request, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    [HttpDelete]
    public async Task<ActionResult<DietaryProfileResponse>> DeleteDietaryProfile(Guid tenantId, Guid householdId, Guid memberId, CancellationToken cancellationToken)
    {
        var response = await _dietaryProfileService.DeleteAsync(tenantId, householdId, memberId, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
        {
            return BadRequest(response);
        }

        if (response.Entity is null)
        {
            return NotFound(response);
        }

        return Ok(response);
    }
}
