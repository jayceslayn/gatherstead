using Gatherstead.Api.Contracts.MemberAttributes;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Security;
using Gatherstead.Api.Services.MemberAttributes;
using Gatherstead.Api.Services.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gatherstead.Api.Controllers;

[ApiController]
[Authorize]
[RequireTenantAccess]
[Route("api/tenants/{tenantId:guid}/households/{householdId:guid}/members/{memberId:guid}/attributes")]
public class MemberAttributesController : ControllerBase
{
    private readonly IMemberAttributeService _memberAttributeService;

    public MemberAttributesController(IMemberAttributeService memberAttributeService)
    {
        _memberAttributeService = memberAttributeService ?? throw new ArgumentNullException(nameof(memberAttributeService));
    }

    [HttpGet]
    public async Task<ActionResult<BaseEntityResponse<IReadOnlyCollection<MemberAttributeDto>>>> GetAttributes(
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
                    return BadRequest(new { error = $"Invalid attribute identifier: '{segment}'." });
                }
                idList.Add(parsed);
            }
            parsedIds = idList;
        }

        var response = await _memberAttributeService.ListAsync(tenantId, householdId, memberId, parsedIds, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    [HttpGet("{attributeId:guid}")]
    public async Task<ActionResult<MemberAttributeResponse>> GetAttribute(Guid tenantId, Guid householdId, Guid memberId, Guid attributeId, CancellationToken cancellationToken)
    {
        var response = await _memberAttributeService.GetAsync(tenantId, householdId, memberId, attributeId, cancellationToken);

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
    public async Task<ActionResult<MemberAttributeResponse>> CreateAttribute(Guid tenantId, Guid householdId, Guid memberId, [FromBody] CreateMemberAttributeRequest request, CancellationToken cancellationToken)
    {
        var response = await _memberAttributeService.CreateAsync(tenantId, householdId, memberId, request, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
        {
            return BadRequest(response);
        }

        return CreatedAtAction(
            nameof(GetAttribute),
            new { tenantId, householdId, memberId, attributeId = response.Entity?.Id },
            response);
    }

    [HttpPut("{attributeId:guid}")]
    public async Task<ActionResult<MemberAttributeResponse>> UpdateAttribute(Guid tenantId, Guid householdId, Guid memberId, Guid attributeId, [FromBody] UpdateMemberAttributeRequest request, CancellationToken cancellationToken)
    {
        var response = await _memberAttributeService.UpdateAsync(tenantId, householdId, memberId, attributeId, request, cancellationToken);

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

    [HttpDelete("{attributeId:guid}")]
    public async Task<ActionResult<MemberAttributeResponse>> DeleteAttribute(Guid tenantId, Guid householdId, Guid memberId, Guid attributeId, CancellationToken cancellationToken)
    {
        var response = await _memberAttributeService.DeleteAsync(tenantId, householdId, memberId, attributeId, cancellationToken);

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
