using Gatherstead.Api.Contracts.Preferences;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Security;
using Gatherstead.Api.Services.Preferences;
using Gatherstead.Api.Services.Validation;
using Gatherstead.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gatherstead.Api.Controllers;

[ApiController]
[Authorize]
[RequireTenantAccess]
[Route("api/tenants/{tenantId:guid}")]
public class PreferencesController : ControllerBase
{
    private readonly IPreferenceService _preferenceService;

    public PreferencesController(IPreferenceService preferenceService)
    {
        _preferenceService = preferenceService ?? throw new ArgumentNullException(nameof(preferenceService));
    }

    [HttpGet("households/{householdId:guid}/members/{memberId:guid}/settings")]
    public async Task<ActionResult<MemberPreferenceSettingsResponse>> GetMemberSettings(Guid tenantId, Guid householdId, Guid memberId, CancellationToken cancellationToken)
    {
        var response = await _preferenceService.GetMemberSettingsAsync(tenantId, householdId, memberId, cancellationToken);
        if (ServiceValidationHelper.HasErrors(response)) return BadRequest(response);
        if (response.Entity is null) return NotFound(response);
        return Ok(response);
    }

    [HttpPut("households/{householdId:guid}/members/{memberId:guid}/settings")]
    public async Task<ActionResult<MemberPreferenceSettingsResponse>> UpsertMemberSettings(Guid tenantId, Guid householdId, Guid memberId, [FromBody] UpsertMemberPreferenceSettingsRequest request, CancellationToken cancellationToken)
    {
        var response = await _preferenceService.UpsertMemberSettingsAsync(tenantId, householdId, memberId, request, cancellationToken);
        if (ServiceValidationHelper.HasErrors(response)) return BadRequest(response);
        return Ok(response);
    }

    [HttpGet("notification-policy/defaults")]
    public async Task<ActionResult<BaseEntityResponse<IReadOnlyCollection<NotificationPreferenceDto>>>> ListTenantDefaults(Guid tenantId, CancellationToken cancellationToken)
    {
        var response = await _preferenceService.ListTenantDefaultsAsync(tenantId, cancellationToken);
        if (ServiceValidationHelper.HasErrors(response)) return BadRequest(response);
        return Ok(response);
    }

    [HttpPut("notification-policy/defaults")]
    public async Task<ActionResult<NotificationPreferenceResponse>> UpsertTenantDefault(Guid tenantId, [FromBody] UpsertNotificationPreferenceRequest request, CancellationToken cancellationToken)
    {
        var response = await _preferenceService.UpsertTenantDefaultAsync(tenantId, request, cancellationToken);
        if (ServiceValidationHelper.HasErrors(response)) return BadRequest(response);
        return Ok(response);
    }

    [HttpDelete("notification-policy/defaults/{channel}/{category}")]
    public async Task<ActionResult<NotificationPreferenceResponse>> DeleteTenantDefault(Guid tenantId, NotificationChannel channel, NotificationCategory category, CancellationToken cancellationToken)
    {
        var response = await _preferenceService.DeleteTenantDefaultAsync(tenantId, channel, category, cancellationToken);
        if (ServiceValidationHelper.HasErrors(response)) return BadRequest(response);
        if (response.Entity is null) return NotFound(response);
        return Ok(response);
    }

    [HttpGet("households/{householdId:guid}/members/{memberId:guid}/notification-overrides")]
    public async Task<ActionResult<BaseEntityResponse<IReadOnlyCollection<NotificationPreferenceDto>>>> ListMemberOverrides(Guid tenantId, Guid householdId, Guid memberId, CancellationToken cancellationToken)
    {
        var response = await _preferenceService.ListMemberOverridesAsync(tenantId, householdId, memberId, cancellationToken);
        if (ServiceValidationHelper.HasErrors(response)) return BadRequest(response);
        return Ok(response);
    }

    [HttpPut("households/{householdId:guid}/members/{memberId:guid}/notification-overrides")]
    public async Task<ActionResult<NotificationPreferenceResponse>> UpsertMemberOverride(Guid tenantId, Guid householdId, Guid memberId, [FromBody] UpsertNotificationPreferenceRequest request, CancellationToken cancellationToken)
    {
        var response = await _preferenceService.UpsertMemberOverrideAsync(tenantId, householdId, memberId, request, cancellationToken);
        if (ServiceValidationHelper.HasErrors(response)) return BadRequest(response);
        return Ok(response);
    }

    [HttpDelete("households/{householdId:guid}/members/{memberId:guid}/notification-overrides/{channel}/{category}")]
    public async Task<ActionResult<NotificationPreferenceResponse>> DeleteMemberOverride(Guid tenantId, Guid householdId, Guid memberId, NotificationChannel channel, NotificationCategory category, CancellationToken cancellationToken)
    {
        var response = await _preferenceService.DeleteMemberOverrideAsync(tenantId, householdId, memberId, channel, category, cancellationToken);
        if (ServiceValidationHelper.HasErrors(response)) return BadRequest(response);
        if (response.Entity is null) return NotFound(response);
        return Ok(response);
    }

    [HttpGet("households/{householdId:guid}/members/{memberId:guid}/notification-preferences/effective")]
    public async Task<ActionResult<BaseEntityResponse<IReadOnlyCollection<EffectiveNotificationPreferenceDto>>>> ListEffective(Guid tenantId, Guid householdId, Guid memberId, CancellationToken cancellationToken)
    {
        var response = await _preferenceService.ListEffectivePreferencesAsync(tenantId, householdId, memberId, cancellationToken);
        if (ServiceValidationHelper.HasErrors(response)) return BadRequest(response);
        return Ok(response);
    }
}
