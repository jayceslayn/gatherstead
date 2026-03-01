using Gatherstead.Api.Contracts.Addresses;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Security;
using Gatherstead.Api.Services.Addresses;
using Gatherstead.Api.Services.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gatherstead.Api.Controllers;

[ApiController]
[Authorize]
[RequireTenantAccess]
[Route("api/tenants/{tenantId:guid}/households/{householdId:guid}/members/{memberId:guid}/addresses")]
public class AddressesController : ControllerBase
{
    private readonly IAddressService _addressService;

    public AddressesController(IAddressService addressService)
    {
        _addressService = addressService ?? throw new ArgumentNullException(nameof(addressService));
    }

    [HttpGet]
    public async Task<ActionResult<BaseEntityResponse<IReadOnlyCollection<AddressDto>>>> GetAddresses(
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
                    return BadRequest(new { error = $"Invalid address identifier: '{segment}'." });
                }
                idList.Add(parsed);
            }
            parsedIds = idList;
        }

        var response = await _addressService.ListAsync(tenantId, householdId, memberId, parsedIds, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    [HttpGet("{addressId:guid}")]
    public async Task<ActionResult<AddressResponse>> GetAddress(Guid tenantId, Guid householdId, Guid memberId, Guid addressId, CancellationToken cancellationToken)
    {
        var response = await _addressService.GetAsync(tenantId, householdId, memberId, addressId, cancellationToken);

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
    public async Task<ActionResult<AddressResponse>> CreateAddress(Guid tenantId, Guid householdId, Guid memberId, [FromBody] CreateAddressRequest request, CancellationToken cancellationToken)
    {
        var response = await _addressService.CreateAsync(tenantId, householdId, memberId, request, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
        {
            return BadRequest(response);
        }

        return CreatedAtAction(
            nameof(GetAddress),
            new { tenantId, householdId, memberId, addressId = response.Entity?.Id },
            response);
    }

    [HttpPut("{addressId:guid}")]
    public async Task<ActionResult<AddressResponse>> UpdateAddress(Guid tenantId, Guid householdId, Guid memberId, Guid addressId, [FromBody] UpdateAddressRequest request, CancellationToken cancellationToken)
    {
        var response = await _addressService.UpdateAsync(tenantId, householdId, memberId, addressId, request, cancellationToken);

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

    [HttpDelete("{addressId:guid}")]
    public async Task<ActionResult<AddressResponse>> DeleteAddress(Guid tenantId, Guid householdId, Guid memberId, Guid addressId, CancellationToken cancellationToken)
    {
        var response = await _addressService.DeleteAsync(tenantId, householdId, memberId, addressId, cancellationToken);

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
