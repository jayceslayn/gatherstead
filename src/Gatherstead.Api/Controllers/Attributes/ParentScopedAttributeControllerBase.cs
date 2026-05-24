using Gatherstead.Api.Contracts.Attributes;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Security;
using Gatherstead.Api.Services.Attributes;
using Gatherstead.Api.Services.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gatherstead.Api.Controllers.Attributes;

[ApiController]
[Authorize]
[RequireTenantAccess]
public abstract class ParentScopedAttributeControllerBase<TService, TDto, TCreate, TUpdate> : ControllerBase
    where TService : IParentScopedAttributeService<TDto, TCreate, TUpdate>
    where TDto : IAttributeDto
{
    protected TService Service { get; }

    protected ParentScopedAttributeControllerBase(TService service)
    {
        Service = service ?? throw new ArgumentNullException(nameof(service));
    }

    [HttpGet]
    public async Task<ActionResult<BaseEntityResponse<IReadOnlyCollection<TDto>>>> GetAttributes(
        Guid tenantId,
        Guid parentId,
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
                    return BadRequest(new { error = $"Invalid attribute identifier: '{segment}'." });
                idList.Add(parsed);
            }
            parsedIds = idList;
        }

        var response = await Service.ListAsync(tenantId, parentId, parsedIds, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        return Ok(response);
    }

    [HttpGet("{attributeId:guid}")]
    public async Task<ActionResult<BaseEntityResponse<TDto>>> GetAttribute(
        Guid tenantId,
        Guid parentId,
        Guid attributeId,
        CancellationToken cancellationToken)
    {
        var response = await Service.GetAsync(tenantId, parentId, attributeId, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }

    [HttpPost]
    public async Task<ActionResult<BaseEntityResponse<TDto>>> CreateAttribute(
        Guid tenantId,
        Guid parentId,
        [FromBody] TCreate request,
        CancellationToken cancellationToken)
    {
        var response = await Service.CreateAsync(tenantId, parentId, request, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        return CreatedAtAction(
            nameof(GetAttribute),
            new { tenantId, parentId, attributeId = response.Entity?.Id },
            response);
    }

    [HttpPut("{attributeId:guid}")]
    public async Task<ActionResult<BaseEntityResponse<TDto>>> UpdateAttribute(
        Guid tenantId,
        Guid parentId,
        Guid attributeId,
        [FromBody] TUpdate request,
        CancellationToken cancellationToken)
    {
        var response = await Service.UpdateAsync(tenantId, parentId, attributeId, request, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }

    [HttpDelete("{attributeId:guid}")]
    public async Task<ActionResult<BaseEntityResponse<TDto>>> DeleteAttribute(
        Guid tenantId,
        Guid parentId,
        Guid attributeId,
        CancellationToken cancellationToken)
    {
        var response = await Service.DeleteAsync(tenantId, parentId, attributeId, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }
}
