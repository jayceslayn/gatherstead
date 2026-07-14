using Gatherstead.Api.Contracts.Equipment;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Security;
using Gatherstead.Api.Services.Equipment;
using Gatherstead.Api.Services.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gatherstead.Api.Controllers;

[ApiController]
[Authorize]
[RequireTenantAccess]
[Route("api/tenants/{tenantId:guid}/equipment")]
public class EquipmentController : ControllerBase
{
    private readonly IEquipmentService _equipmentService;

    public EquipmentController(IEquipmentService equipmentService)
    {
        _equipmentService = equipmentService ?? throw new ArgumentNullException(nameof(equipmentService));
    }

    [HttpGet]
    public async Task<ActionResult<BaseEntityResponse<IReadOnlyCollection<EquipmentDto>>>> GetEquipment(
        Guid tenantId,
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
                    return BadRequest(new { error = $"Invalid equipment identifier: '{segment}'." });
                idList.Add(parsed);
            }
            parsedIds = idList;
        }

        var response = await _equipmentService.ListAsync(tenantId, parsedIds, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return this.ToErrorResult(response);

        return Ok(response);
    }

    [HttpGet("{equipmentId:guid}")]
    public async Task<ActionResult<EquipmentResponse>> GetEquipmentItem(Guid tenantId, Guid equipmentId, CancellationToken cancellationToken)
    {
        var response = await _equipmentService.GetAsync(tenantId, equipmentId, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return this.ToErrorResult(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }

    [HttpPost]
    public async Task<ActionResult<EquipmentResponse>> CreateEquipmentItem(Guid tenantId, [FromBody] CreateEquipmentRequest request, CancellationToken cancellationToken)
    {
        var response = await _equipmentService.CreateAsync(tenantId, request, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return this.ToErrorResult(response);

        return CreatedAtAction(
            nameof(GetEquipmentItem),
            new { tenantId, equipmentId = response.Entity?.Id },
            response);
    }

    [HttpPut("{equipmentId:guid}")]
    public async Task<ActionResult<EquipmentResponse>> UpdateEquipmentItem(Guid tenantId, Guid equipmentId, [FromBody] UpdateEquipmentRequest request, CancellationToken cancellationToken)
    {
        var response = await _equipmentService.UpdateAsync(tenantId, equipmentId, request, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return this.ToErrorResult(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }

    [HttpDelete("{equipmentId:guid}")]
    public async Task<ActionResult<EquipmentResponse>> DeleteEquipmentItem(Guid tenantId, Guid equipmentId, CancellationToken cancellationToken)
    {
        var response = await _equipmentService.DeleteAsync(tenantId, equipmentId, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return this.ToErrorResult(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }
}
