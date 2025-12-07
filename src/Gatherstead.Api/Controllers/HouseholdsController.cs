using System.ComponentModel.DataAnnotations;
using Gatherstead.Db;
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
        var households = await _dbContext.Households
            .AsNoTracking()
            .Where(h => h.TenantId == tenantId && !h.IsDeleted)
            .Select(h => new HouseholdResponse(h.Id, h.TenantId, h.Name))
            .ToListAsync(cancellationToken);

        return Ok(households);
    }

    [HttpGet("{householdId:guid}")]
    public async Task<ActionResult<HouseholdResponse>> GetHousehold(Guid tenantId, Guid householdId, CancellationToken cancellationToken)
    {
        var household = await _dbContext.Households
            .AsNoTracking()
            .Where(h => h.TenantId == tenantId && h.Id == householdId && !h.IsDeleted)
            .Select(h => new HouseholdResponse(h.Id, h.TenantId, h.Name))
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
        var normalizedName = request.Name.Trim();
        if (string.IsNullOrEmpty(normalizedName))
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

        var response = new HouseholdResponse(household.Id, household.TenantId, household.Name);

        return CreatedAtAction(nameof(GetHousehold), new { tenantId, householdId = household.Id }, response);
    }
}

public record HouseholdResponse(Guid Id, Guid TenantId, string Name);

public class CreateHouseholdRequest
{
    [Required]
    [StringLength(200)]
    public string Name { get; init; } = string.Empty;
}
