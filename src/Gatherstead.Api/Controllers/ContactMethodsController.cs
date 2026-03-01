using Gatherstead.Api.Contracts.ContactMethods;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Security;
using Gatherstead.Api.Services.ContactMethods;
using Gatherstead.Api.Services.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gatherstead.Api.Controllers;

[ApiController]
[Authorize]
[RequireTenantAccess]
[Route("api/tenants/{tenantId:guid}/households/{householdId:guid}/members/{memberId:guid}/contacts")]
public class ContactMethodsController : ControllerBase
{
    private readonly IContactMethodService _contactMethodService;

    public ContactMethodsController(IContactMethodService contactMethodService)
    {
        _contactMethodService = contactMethodService ?? throw new ArgumentNullException(nameof(contactMethodService));
    }

    [HttpGet]
    public async Task<ActionResult<BaseEntityResponse<IReadOnlyCollection<ContactMethodDto>>>> GetContactMethods(
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
                    return BadRequest(new { error = $"Invalid contact method identifier: '{segment}'." });
                }
                idList.Add(parsed);
            }
            parsedIds = idList;
        }

        var response = await _contactMethodService.ListAsync(tenantId, householdId, memberId, parsedIds, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    [HttpGet("{contactMethodId:guid}")]
    public async Task<ActionResult<ContactMethodResponse>> GetContactMethod(Guid tenantId, Guid householdId, Guid memberId, Guid contactMethodId, CancellationToken cancellationToken)
    {
        var response = await _contactMethodService.GetAsync(tenantId, householdId, memberId, contactMethodId, cancellationToken);

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
    public async Task<ActionResult<ContactMethodResponse>> CreateContactMethod(Guid tenantId, Guid householdId, Guid memberId, [FromBody] CreateContactMethodRequest request, CancellationToken cancellationToken)
    {
        var response = await _contactMethodService.CreateAsync(tenantId, householdId, memberId, request, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
        {
            return BadRequest(response);
        }

        return CreatedAtAction(
            nameof(GetContactMethod),
            new { tenantId, householdId, memberId, contactMethodId = response.Entity?.Id },
            response);
    }

    [HttpPut("{contactMethodId:guid}")]
    public async Task<ActionResult<ContactMethodResponse>> UpdateContactMethod(Guid tenantId, Guid householdId, Guid memberId, Guid contactMethodId, [FromBody] UpdateContactMethodRequest request, CancellationToken cancellationToken)
    {
        var response = await _contactMethodService.UpdateAsync(tenantId, householdId, memberId, contactMethodId, request, cancellationToken);

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

    [HttpDelete("{contactMethodId:guid}")]
    public async Task<ActionResult<ContactMethodResponse>> DeleteContactMethod(Guid tenantId, Guid householdId, Guid memberId, Guid contactMethodId, CancellationToken cancellationToken)
    {
        var response = await _contactMethodService.DeleteAsync(tenantId, householdId, memberId, contactMethodId, cancellationToken);

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
