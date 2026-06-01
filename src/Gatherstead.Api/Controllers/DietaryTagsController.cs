using Gatherstead.Api.Contracts.DietaryTags;
using Gatherstead.Api.Services.DietaryTags;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gatherstead.Api.Controllers;

/// <summary>
/// App-wide dietary tag reference data. No tenant scoping — tags are shared across all tenants.
/// Requires authentication only (any authenticated user may fetch the tag list).
/// </summary>
[ApiController]
[Authorize]
[Route("api/dietary-tags")]
public class DietaryTagsController : ControllerBase
{
    private readonly IDietaryTagService _dietaryTagService;

    public DietaryTagsController(IDietaryTagService dietaryTagService)
    {
        _dietaryTagService = dietaryTagService ?? throw new ArgumentNullException(nameof(dietaryTagService));
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DietaryTagDto>>> GetDietaryTags(
        CancellationToken cancellationToken)
    {
        var tags = await _dietaryTagService.ListActiveAsync(cancellationToken);
        return Ok(tags);
    }
}
