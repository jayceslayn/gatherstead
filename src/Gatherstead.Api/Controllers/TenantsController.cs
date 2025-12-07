using Gatherstead.Api.Contracts.Tenants;
using Gatherstead.Db;
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

    public TenantsController(GathersteadDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TenantSummary>>> GetTenants(CancellationToken cancellationToken)
    {
        var tenants = await _dbContext.Tenants
            .AsNoTracking()
            .Select(t => new TenantSummary(t.Id, t.Name))
            .ToListAsync(cancellationToken);

        return Ok(tenants);
    }
}
