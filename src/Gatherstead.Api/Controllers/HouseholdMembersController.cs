using Gatherstead.Api.Contracts.HouseholdMembers;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Security;
using Gatherstead.Api.Services.HouseholdMembers;
using Gatherstead.Api.Services.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gatherstead.Api.Controllers;

[ApiController]
[Authorize]
[RequireTenantAccess]
[Route("api/tenants/{tenantId:guid}/households/{householdId:guid}/members")]
public class HouseholdMembersController : ControllerBase
{
    private readonly IHouseholdMemberService _householdMemberService;

    public HouseholdMembersController(IHouseholdMemberService householdMemberService)
    {
        _householdMemberService = householdMemberService ?? throw new ArgumentNullException(nameof(householdMemberService));
    }

    [HttpGet]
    public async Task<ActionResult<BaseEntityResponse<IReadOnlyCollection<HouseholdMemberDto>>>> GetMembers(
        Guid tenantId,
        Guid householdId,
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
                {
                    return BadRequest(new { error = $"Invalid member identifier: '{segment}'." });
                }
                idList.Add(parsed);
            }
            parsedIds = idList;
        }

        var response = await _householdMemberService.ListAsync(tenantId, householdId, parsedIds, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    [HttpGet("{memberId:guid}")]
    public async Task<ActionResult<HouseholdMemberResponse>> GetMember(Guid tenantId, Guid householdId, Guid memberId, CancellationToken cancellationToken)
    {
        var response = await _householdMemberService.GetAsync(tenantId, householdId, memberId, cancellationToken);

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

    [HttpPost]
    public async Task<ActionResult<HouseholdMemberResponse>> CreateMember(Guid tenantId, Guid householdId, [FromBody] CreateHouseholdMemberRequest request, CancellationToken cancellationToken)
    {
        var response = await _householdMemberService.CreateAsync(tenantId, householdId, request, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
        {
            return BadRequest(response);
        }

        return CreatedAtAction(
            nameof(GetMember),
            new { tenantId, householdId, memberId = response.Entity?.Id },
            response);
    }

    [HttpPut("{memberId:guid}")]
    public async Task<ActionResult<HouseholdMemberResponse>> UpdateMember(Guid tenantId, Guid householdId, Guid memberId, [FromBody] UpdateHouseholdMemberRequest request, CancellationToken cancellationToken)
    {
        var response = await _householdMemberService.UpdateAsync(tenantId, householdId, memberId, request, cancellationToken);

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

    [HttpDelete("{memberId:guid}")]
    public async Task<ActionResult<HouseholdMemberResponse>> DeleteMember(Guid tenantId, Guid householdId, Guid memberId, CancellationToken cancellationToken)
    {
        var response = await _householdMemberService.DeleteAsync(tenantId, householdId, memberId, cancellationToken);

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
