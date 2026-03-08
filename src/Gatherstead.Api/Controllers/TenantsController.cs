using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Contracts.Tenants;
using Gatherstead.Api.Security;
using Gatherstead.Api.Services.Tenants;
using Gatherstead.Api.Services.Validation;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gatherstead.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/tenants")]
public class TenantsController : ControllerBase
{
    private readonly ITenantService _tenantService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IAppAdminContext _appAdminContext;

    public TenantsController(
        ITenantService tenantService,
        ICurrentUserContext currentUserContext,
        IAppAdminContext appAdminContext)
    {
        _tenantService = tenantService ?? throw new ArgumentNullException(nameof(tenantService));
        _currentUserContext = currentUserContext ?? throw new ArgumentNullException(nameof(currentUserContext));
        _appAdminContext = appAdminContext ?? throw new ArgumentNullException(nameof(appAdminContext));
    }

    [HttpGet]
    public async Task<ActionResult<BaseEntityResponse<IReadOnlyCollection<TenantSummary>>>> GetTenants(
        [FromQuery] string? ids,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserContext.UserId;
        if (!userId.HasValue)
        {
            return Unauthorized(new { error = "Authentication required." });
        }

        IEnumerable<Guid>? parsedIds = null;
        if (!string.IsNullOrWhiteSpace(ids))
        {
            var idList = new List<Guid>();
            foreach (var segment in ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!Guid.TryParse(segment, out var parsed))
                {
                    return BadRequest(new { error = $"Invalid tenant identifier: '{segment}'." });
                }
                idList.Add(parsed);
            }
            parsedIds = idList;
        }

        // App Admins see all tenants; regular users see only their own
        var isAppAdmin = await _appAdminContext.IsAppAdminAsync(cancellationToken);
        var response = isAppAdmin == true
            ? await _tenantService.ListAllAsync(parsedIds, cancellationToken)
            : await _tenantService.ListAsync(userId.Value, parsedIds, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    [HttpGet("{tenantId:guid}")]
    [RequireTenantAccess]
    public async Task<ActionResult<TenantResponse>> GetTenant(Guid tenantId, CancellationToken cancellationToken)
    {
        var response = await _tenantService.GetAsync(tenantId, cancellationToken);

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
    [RequireAppAdmin]
    public async Task<ActionResult<TenantResponse>> CreateTenant([FromBody] CreateTenantRequest request, CancellationToken cancellationToken)
    {
        var response = await _tenantService.CreateAsync(request, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
        {
            return BadRequest(response);
        }

        return CreatedAtAction(
            nameof(GetTenant),
            new { tenantId = response.Entity?.Id },
            response);
    }

    [HttpPut("{tenantId:guid}")]
    [RequireTenantAccess(TenantRole.Owner)]
    public async Task<ActionResult<TenantResponse>> UpdateTenant(Guid tenantId, [FromBody] UpdateTenantRequest request, CancellationToken cancellationToken)
    {
        var response = await _tenantService.UpdateAsync(tenantId, request, cancellationToken);

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

    [HttpDelete("{tenantId:guid}")]
    [RequireTenantAccess(TenantRole.Owner)]
    public async Task<ActionResult<TenantResponse>> DeleteTenant(Guid tenantId, CancellationToken cancellationToken)
    {
        var response = await _tenantService.DeleteAsync(tenantId, cancellationToken);

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
