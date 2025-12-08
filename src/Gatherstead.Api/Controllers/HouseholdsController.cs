using System.Linq;
using Gatherstead.Db;
using Gatherstead.Api.Contracts.Households;
using Gatherstead.Db.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/tenants/{tenantId:guid}/households")]
public class HouseholdsController : ControllerBase
{
    private readonly GathersteadDbContext _dbContext;

    public HouseholdsController(GathersteadDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<HouseholdResponse>>> GetHouseholds(Guid tenantId, CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty)
        {
            return BadRequest("A valid tenant identifier is required.");
        }

        var households = await _dbContext.Households
            .AsNoTracking()
            .Where(h => h.TenantId == tenantId)
            .Select(h => new HouseholdResponse(h.Id, h.TenantId, h.Name, h.CreatedAt, h.UpdatedAt, h.DeletedAt))
            .ToListAsync(cancellationToken);

        return Ok(households);
    }

    [HttpGet("{householdId:guid}")]
    public async Task<ActionResult<HouseholdResponse>> GetHousehold(Guid tenantId, Guid householdId, CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty)
        {
            return BadRequest("A valid tenant identifier is required.");
        }

        var household = await _dbContext.Households
            .AsNoTracking()
            .Where(h => h.Id == householdId && h.TenantId == tenantId)
            .Select(h => new HouseholdResponse(h.Id, h.TenantId, h.Name, h.CreatedAt, h.UpdatedAt, h.DeletedAt))
            .SingleOrDefaultAsync(cancellationToken);

        if (household is null)
        {
            return NotFound();
        }

        return Ok(household);
    }

    [HttpPost]
    public async Task<ActionResult<HouseholdResponse>> CreateHousehold(Guid tenantId, [FromBody] CreateHouseholdRequest request, CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty)
        {
            return BadRequest("A valid tenant identifier is required.");
        }

        var normalizedName = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            ModelState.AddModelError(nameof(request.Name), "Name is required.");
            return ValidationProblem(ModelState);
        }

        var household = new Household
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = normalizedName
        };

        _dbContext.Households.Add(household);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = new HouseholdResponse(household.Id, household.TenantId, household.Name, household.CreatedAt, household.UpdatedAt, household.DeletedAt);

        return CreatedAtAction(nameof(GetHousehold), new { tenantId, householdId = household.Id }, response);
    }

    [HttpPut("{householdId:guid}")]
    public async Task<ActionResult<HouseholdResponse>> UpdateHousehold(Guid tenantId, Guid householdId, [FromBody] UpdateHouseholdRequest request, CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty)
        {
            return BadRequest("A valid tenant identifier is required.");
        }

        var normalizedName = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            ModelState.AddModelError(nameof(request.Name), "Name is required.");
            return ValidationProblem(ModelState);
        }

        var household = await _dbContext.Households
            .Where(h => h.Id == householdId && h.TenantId == tenantId)
            .SingleOrDefaultAsync(cancellationToken);

        if (household is null)
        {
            return NotFound();
        }

        household.Name = normalizedName;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = new HouseholdResponse(household.Id, household.TenantId, household.Name, household.CreatedAt, household.UpdatedAt, household.DeletedAt);

        return Ok(response);
    }
}
