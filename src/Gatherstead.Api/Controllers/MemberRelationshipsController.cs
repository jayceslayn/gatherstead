using Gatherstead.Api.Contracts.MemberRelationships;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Security;
using Gatherstead.Api.Services.MemberRelationships;
using Gatherstead.Api.Services.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gatherstead.Api.Controllers;

[ApiController]
[Authorize]
[RequireTenantAccess]
[Route("api/tenants/{tenantId:guid}/households/{householdId:guid}/members/{memberId:guid}/relationships")]
public class MemberRelationshipsController : ControllerBase
{
    private readonly IMemberRelationshipService _memberRelationshipService;

    public MemberRelationshipsController(IMemberRelationshipService memberRelationshipService)
    {
        _memberRelationshipService = memberRelationshipService ?? throw new ArgumentNullException(nameof(memberRelationshipService));
    }

    [HttpGet]
    public async Task<ActionResult<BaseEntityResponse<IReadOnlyCollection<MemberRelationshipDto>>>> GetRelationships(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
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
                    return BadRequest(new { error = $"Invalid relationship identifier: '{segment}'." });
                }
                idList.Add(parsed);
            }
            parsedIds = idList;
        }

        var response = await _memberRelationshipService.ListAsync(tenantId, householdId, memberId, parsedIds, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    [HttpGet("{relationshipId:guid}")]
    public async Task<ActionResult<MemberRelationshipResponse>> GetRelationship(Guid tenantId, Guid householdId, Guid memberId, Guid relationshipId, CancellationToken cancellationToken)
    {
        var response = await _memberRelationshipService.GetAsync(tenantId, householdId, memberId, relationshipId, cancellationToken);

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
    public async Task<ActionResult<MemberRelationshipResponse>> CreateRelationship(Guid tenantId, Guid householdId, Guid memberId, [FromBody] CreateMemberRelationshipRequest request, CancellationToken cancellationToken)
    {
        var response = await _memberRelationshipService.CreateAsync(tenantId, householdId, memberId, request, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
        {
            return BadRequest(response);
        }

        return CreatedAtAction(
            nameof(GetRelationship),
            new { tenantId, householdId, memberId, relationshipId = response.Entity?.Id },
            response);
    }

    [HttpPut("{relationshipId:guid}")]
    public async Task<ActionResult<MemberRelationshipResponse>> UpdateRelationship(Guid tenantId, Guid householdId, Guid memberId, Guid relationshipId, [FromBody] UpdateMemberRelationshipRequest request, CancellationToken cancellationToken)
    {
        var response = await _memberRelationshipService.UpdateAsync(tenantId, householdId, memberId, relationshipId, request, cancellationToken);

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

    [HttpDelete("{relationshipId:guid}")]
    public async Task<ActionResult<MemberRelationshipResponse>> DeleteRelationship(Guid tenantId, Guid householdId, Guid memberId, Guid relationshipId, CancellationToken cancellationToken)
    {
        var response = await _memberRelationshipService.DeleteAsync(tenantId, householdId, memberId, relationshipId, cancellationToken);

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
