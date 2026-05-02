using Gatherstead.Api.Contracts.Preferences;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Services.Preferences;
using Gatherstead.Api.Services.Validation;
using Gatherstead.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gatherstead.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/users/me/notification-preferences")]
public class UserPreferencesController : ControllerBase
{
    private readonly IPreferenceService _preferenceService;

    public UserPreferencesController(IPreferenceService preferenceService)
    {
        _preferenceService = preferenceService ?? throw new ArgumentNullException(nameof(preferenceService));
    }


    [HttpGet("settings")]
    public async Task<ActionResult<UserPreferenceSettingsResponse>> GetSettings(CancellationToken cancellationToken)
    {
        var response = await _preferenceService.GetUserPreferenceSettingsAsync(cancellationToken);
        if (ServiceValidationHelper.HasErrors(response)) return BadRequest(response);
        if (response.Entity is null) return NotFound(response);
        return Ok(response);
    }

    [HttpPut("settings")]
    public async Task<ActionResult<UserPreferenceSettingsResponse>> UpsertSettings([FromBody] UpsertUserPreferenceSettingsRequest request, CancellationToken cancellationToken)
    {
        var response = await _preferenceService.UpsertUserPreferenceSettingsAsync(request, cancellationToken);
        if (ServiceValidationHelper.HasErrors(response)) return BadRequest(response);
        return Ok(response);
    }

    [HttpGet]
    public async Task<ActionResult<BaseEntityResponse<IReadOnlyCollection<UserNotificationPreferenceDto>>>> List(CancellationToken cancellationToken)
    {
        var response = await _preferenceService.ListUserPreferencesAsync(cancellationToken);
        if (ServiceValidationHelper.HasErrors(response)) return BadRequest(response);
        return Ok(response);
    }

    [HttpPut]
    public async Task<ActionResult<UserNotificationPreferenceResponse>> Upsert([FromBody] UpsertNotificationPreferenceRequest request, CancellationToken cancellationToken)
    {
        var response = await _preferenceService.UpsertUserPreferenceAsync(request, cancellationToken);
        if (ServiceValidationHelper.HasErrors(response)) return BadRequest(response);
        return Ok(response);
    }

    [HttpDelete("{channel}/{category}")]
    public async Task<ActionResult<UserNotificationPreferenceResponse>> Delete(NotificationChannel channel, NotificationCategory category, CancellationToken cancellationToken)
    {
        var response = await _preferenceService.DeleteUserPreferenceAsync(channel, category, cancellationToken);
        if (ServiceValidationHelper.HasErrors(response)) return BadRequest(response);
        if (response.Entity is null) return NotFound(response);
        return Ok(response);
    }
}
