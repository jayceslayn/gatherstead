using Gatherstead.Api.Contracts.Tenants;
using Gatherstead.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/tenants")]
public class TenantsController : ControllerBase
{
    private readonly GathersteadDbContext _dbContext;
    private readonly ICurrentUserContext _currentUserContext;

    public TenantsController(GathersteadDbContext dbContext, ICurrentUserContext currentUserContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentUserContext = currentUserContext ?? throw new ArgumentNullException(nameof(currentUserContext));
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TenantSummary>>> GetTenants(CancellationToken cancellationToken)
    {
        var userId = _currentUserContext.UserId;
        if (!userId.HasValue)
        {
            return Unauthorized(new { error = "Authentication required." });
        }

        // Only return tenants the user has access to via TenantUser junction table
        var tenants = await _dbContext.TenantUsers
            .AsNoTracking()
            .Where(tu => tu.UserId == userId.Value)
            .Select(tu => new TenantSummary(tu.TenantId, tu.Tenant!.Name))
            .ToListAsync(cancellationToken);

        return Ok(tenants);
    }
}
