using Gatherstead.Api.Contracts.AgeBands;
using Gatherstead.Api.Services.AgeBands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gatherstead.Api.Controllers;

/// <summary>
/// App-wide age band reference data. No tenant scoping — bands are shared across all tenants.
/// Requires authentication only (any authenticated user may fetch the options).
/// </summary>
[ApiController]
[Authorize]
[Route("api/age-bands")]
public class AgeBandsController : ControllerBase
{
    private readonly IAgeBandService _ageBandService;

    public AgeBandsController(IAgeBandService ageBandService)
    {
        _ageBandService = ageBandService ?? throw new ArgumentNullException(nameof(ageBandService));
    }

    [HttpGet]
    public ActionResult<IReadOnlyList<AgeBandOptionDto>> GetAgeBands()
    {
        return Ok(_ageBandService.ListOptions());
    }
}
