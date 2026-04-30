using System.Text.RegularExpressions;
using Gatherstead.Api.Contracts.Preferences;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.Validation;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Api.Services.Preferences;

public class PreferenceService : IPreferenceService
{
    private static readonly Regex Bcp47Pattern = new("^[A-Za-z]{2,3}(-[A-Za-z0-9]{2,8})*$", RegexOptions.Compiled);
    private readonly GathersteadDbContext _dbContext;
    private readonly ICurrentTenantContext _currentTenantContext;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IMemberAuthorizationService _memberAuthorizationService;

    public PreferenceService(
        GathersteadDbContext dbContext,
        ICurrentTenantContext currentTenantContext,
        ICurrentUserContext currentUserContext,
        IMemberAuthorizationService memberAuthorizationService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentTenantContext = currentTenantContext ?? throw new ArgumentNullException(nameof(currentTenantContext));
        _currentUserContext = currentUserContext ?? throw new ArgumentNullException(nameof(currentUserContext));
        _memberAuthorizationService = memberAuthorizationService ?? throw new ArgumentNullException(nameof(memberAuthorizationService));
    }

    public async Task<MemberPreferenceSettingsResponse> GetMemberSettingsAsync(Guid tenantId, Guid householdId, Guid memberId, CancellationToken cancellationToken = default)
    {
        var response = new MemberPreferenceSettingsResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        if (!await ServiceGuards.RequireMemberExistsAsync(response, _dbContext, tenantId, householdId, memberId, cancellationToken))
            return response;

        var settings = await _dbContext.MemberPreferenceSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(m => m.TenantId == tenantId && m.HouseholdMemberId == memberId, cancellationToken);

        if (settings is null)
        {
            response.AddResponseMessage(MessageType.INFO, "Member preference settings not found.");
            return response;
        }

        response.SetSuccess(Map(settings));
        return response;
    }

    public async Task<MemberPreferenceSettingsResponse> UpsertMemberSettingsAsync(Guid tenantId, Guid householdId, Guid memberId, UpsertMemberPreferenceSettingsRequest request, CancellationToken cancellationToken = default)
    {
        var response = new MemberPreferenceSettingsResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!ServiceGuards.RequireRequest(request, "upsert member settings", response))
            return response;
        if (!await ServiceGuards.AuthorizeMemberEditAsync(response, _memberAuthorizationService, tenantId, householdId, memberId, cancellationToken))
            return response;
        if (!await ServiceGuards.RequireMemberExistsAsync(response, _dbContext, tenantId, householdId, memberId, cancellationToken))
            return response;

        var preferredLanguage = request.PreferredLanguage.Trim();
        var timeZone = request.TimeZone.Trim();

        if (!Bcp47Pattern.IsMatch(preferredLanguage))
        {
            response.AddResponseMessage(MessageType.ERROR, "PreferredLanguage must be a valid BCP-47 language tag.");
            return response;
        }

        if (!IsPlausibleIanaTimeZone(timeZone))
        {
            response.AddResponseMessage(MessageType.ERROR, "TimeZone must be a valid IANA time zone identifier.");
            return response;
        }

        if (!await ServiceGuards.RequireMemberExistsAsync(response, _dbContext, tenantId, householdId, memberId, cancellationToken))
            return response;

        var settings = await _dbContext.MemberPreferenceSettings
            .SingleOrDefaultAsync(m => m.TenantId == tenantId && m.HouseholdMemberId == memberId, cancellationToken);

        if (settings is null)
        {
            settings = new MemberPreferenceSettings
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                HouseholdMemberId = memberId,
                PreferredLanguage = preferredLanguage,
                TimeZone = timeZone,
            };

            _dbContext.MemberPreferenceSettings.Add(settings);
        }
        else
        {
            settings.PreferredLanguage = preferredLanguage;
            settings.TimeZone = timeZone;
            settings.IsDeleted = false;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        response.SetSuccess(Map(settings));
        return response;
    }

    public async Task<BaseEntityResponse<IReadOnlyCollection<NotificationPreferenceDto>>> ListTenantDefaultsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var response = new BaseEntityResponse<IReadOnlyCollection<NotificationPreferenceDto>>();
        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        var items = await _dbContext.TenantNotificationPolicies
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId)
            .Select(p => new NotificationPreferenceDto(p.Id, p.TenantId, null, p.Channel, p.Category, p.Mode, p.CreatedAt, p.UpdatedAt, p.IsDeleted, p.DeletedAt, p.DeletedByUserId))
            .ToListAsync(cancellationToken);

        return BaseEntityResponse<IReadOnlyCollection<NotificationPreferenceDto>>.SuccessfulResponse(items);
    }

    public async Task<NotificationPreferenceResponse> UpsertTenantDefaultAsync(Guid tenantId, UpsertNotificationPreferenceRequest request, CancellationToken cancellationToken = default)
    {
        var response = new NotificationPreferenceResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!ServiceGuards.RequireRequest(request, "upsert tenant notification default", response))
            return response;
        if (!await ServiceGuards.AuthorizeTenantManageAsync(response, _memberAuthorizationService, tenantId, cancellationToken))
            return response;

        var existing = await _dbContext.TenantNotificationPolicies
            .SingleOrDefaultAsync(p => p.TenantId == tenantId && p.Channel == request.Channel && p.Category == request.Category, cancellationToken);

        if (existing is null)
        {
            existing = new TenantNotificationPolicy
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Channel = request.Channel,
                Category = request.Category,
                Mode = request.Mode,
            };
            _dbContext.TenantNotificationPolicies.Add(existing);
        }
        else
        {
            existing.Mode = request.Mode;
            existing.IsDeleted = false;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        response.SetSuccess(Map(existing));
        return response;
    }

    public async Task<NotificationPreferenceResponse> DeleteTenantDefaultAsync(Guid tenantId, NotificationChannel channel, NotificationCategory category, CancellationToken cancellationToken = default)
    {
        var response = new NotificationPreferenceResponse();
        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!await ServiceGuards.AuthorizeTenantManageAsync(response, _memberAuthorizationService, tenantId, cancellationToken))
            return response;

        var existing = await _dbContext.TenantNotificationPolicies
            .SingleOrDefaultAsync(p => p.TenantId == tenantId && p.Channel == channel && p.Category == category, cancellationToken);

        if (existing is null)
        {
            response.AddResponseMessage(MessageType.WARNING, "Tenant notification default not found.");
            return response;
        }

        existing.IsDeleted = true;
        await _dbContext.SaveChangesAsync(cancellationToken);
        response.SetSuccess(Map(existing));
        return response;
    }

    public async Task<BaseEntityResponse<IReadOnlyCollection<NotificationPreferenceDto>>> ListMemberOverridesAsync(Guid tenantId, Guid householdId, Guid memberId, CancellationToken cancellationToken = default)
    {
        var response = new BaseEntityResponse<IReadOnlyCollection<NotificationPreferenceDto>>();
        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        if (!await ServiceGuards.RequireMemberExistsAsync(response, _dbContext, tenantId, householdId, memberId, cancellationToken))
            return response;

        var items = await _dbContext.MemberNotificationPreferences
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.HouseholdMemberId == memberId)
            .Select(p => new NotificationPreferenceDto(p.Id, p.TenantId, p.HouseholdMemberId, p.Channel, p.Category, p.Mode, p.CreatedAt, p.UpdatedAt, p.IsDeleted, p.DeletedAt, p.DeletedByUserId))
            .ToListAsync(cancellationToken);

        return BaseEntityResponse<IReadOnlyCollection<NotificationPreferenceDto>>.SuccessfulResponse(items);
    }

    public async Task<NotificationPreferenceResponse> UpsertMemberOverrideAsync(Guid tenantId, Guid householdId, Guid memberId, UpsertNotificationPreferenceRequest request, CancellationToken cancellationToken = default)
    {
        var response = new NotificationPreferenceResponse();
        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!ServiceGuards.RequireRequest(request, "upsert member notification override", response))
            return response;
        if (!await ServiceGuards.AuthorizeMemberEditAsync(response, _memberAuthorizationService, tenantId, householdId, memberId, cancellationToken))
            return response;
        if (!await ServiceGuards.RequireMemberExistsAsync(response, _dbContext, tenantId, householdId, memberId, cancellationToken))
            return response;

        var existing = await _dbContext.MemberNotificationPreferences
            .SingleOrDefaultAsync(p => p.TenantId == tenantId && p.HouseholdMemberId == memberId && p.Channel == request.Channel && p.Category == request.Category, cancellationToken);

        if (existing is null)
        {
            existing = new MemberNotificationPreference
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                HouseholdMemberId = memberId,
                Channel = request.Channel,
                Category = request.Category,
                Mode = request.Mode,
            };
            _dbContext.MemberNotificationPreferences.Add(existing);
        }
        else
        {
            existing.Mode = request.Mode;
            existing.IsDeleted = false;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        response.SetSuccess(Map(existing));
        return response;
    }

    public async Task<NotificationPreferenceResponse> DeleteMemberOverrideAsync(Guid tenantId, Guid householdId, Guid memberId, NotificationChannel channel, NotificationCategory category, CancellationToken cancellationToken = default)
    {
        var response = new NotificationPreferenceResponse();
        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!await ServiceGuards.AuthorizeMemberEditAsync(response, _memberAuthorizationService, tenantId, householdId, memberId, cancellationToken))
            return response;

        var existing = await _dbContext.MemberNotificationPreferences
            .SingleOrDefaultAsync(p => p.TenantId == tenantId && p.HouseholdMemberId == memberId && p.Channel == channel && p.Category == category, cancellationToken);

        if (existing is null)
        {
            response.AddResponseMessage(MessageType.WARNING, "Member notification override not found.");
            return response;
        }

        existing.IsDeleted = true;
        await _dbContext.SaveChangesAsync(cancellationToken);
        response.SetSuccess(Map(existing));
        return response;
    }

    public async Task<BaseEntityResponse<IReadOnlyCollection<EffectiveNotificationPreferenceDto>>> ListEffectivePreferencesAsync(Guid tenantId, Guid householdId, Guid memberId, CancellationToken cancellationToken = default)
    {
        var response = new BaseEntityResponse<IReadOnlyCollection<EffectiveNotificationPreferenceDto>>();
        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        if (!await ServiceGuards.RequireMemberExistsAsync(response, _dbContext, tenantId, householdId, memberId, cancellationToken))
            return response;

        var defaults = await _dbContext.TenantNotificationPolicies
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        var overrides = await _dbContext.MemberNotificationPreferences
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.HouseholdMemberId == memberId)
            .ToListAsync(cancellationToken);

        var map = defaults.ToDictionary(
            p => (p.Channel, p.Category),
            p => new EffectiveNotificationPreferenceDto(p.Channel, p.Category, p.Mode, "TenantDefault"));

        foreach (var item in overrides)
            map[(item.Channel, item.Category)] = new EffectiveNotificationPreferenceDto(item.Channel, item.Category, item.Mode, "MemberOverride");

        return BaseEntityResponse<IReadOnlyCollection<EffectiveNotificationPreferenceDto>>.SuccessfulResponse(
            map.Values
                .OrderBy(p => p.Channel)
                .ThenBy(p => p.Category)
                .ToArray());
    }


    public async Task<BaseEntityResponse<IReadOnlyCollection<UserNotificationPreferenceDto>>> ListUserPreferencesAsync(CancellationToken cancellationToken = default)
    {
        var response = new BaseEntityResponse<IReadOnlyCollection<UserNotificationPreferenceDto>>();
        if (_currentUserContext.UserId is null)
        {
            response.AddResponseMessage(MessageType.ERROR, "User context is required.");
            return response;
        }

        var userId = _currentUserContext.UserId.Value;

        var items = await _dbContext.UserNotificationPreferences
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => new UserNotificationPreferenceDto(p.Id, p.UserId, p.Channel, p.Category, p.Mode, p.CreatedAt, p.UpdatedAt, p.IsDeleted, p.DeletedAt, p.DeletedByUserId))
            .ToListAsync(cancellationToken);

        return BaseEntityResponse<IReadOnlyCollection<UserNotificationPreferenceDto>>.SuccessfulResponse(items);
    }

    public async Task<UserNotificationPreferenceResponse> UpsertUserPreferenceAsync(UpsertNotificationPreferenceRequest request, CancellationToken cancellationToken = default)
    {
        var response = new UserNotificationPreferenceResponse();
        if (!ServiceGuards.RequireRequest(request, "upsert user notification preference", response))
            return response;
        if (_currentUserContext.UserId is null)
        {
            response.AddResponseMessage(MessageType.ERROR, "User context is required.");
            return response;
        }

        var userId = _currentUserContext.UserId.Value;

        var existing = await _dbContext.UserNotificationPreferences
            .SingleOrDefaultAsync(p => p.UserId == userId && p.Channel == request.Channel && p.Category == request.Category, cancellationToken);

        if (existing is null)
        {
            existing = new UserNotificationPreference
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Channel = request.Channel,
                Category = request.Category,
                Mode = request.Mode,
            };
            _dbContext.UserNotificationPreferences.Add(existing);
        }
        else
        {
            existing.Mode = request.Mode;
            existing.IsDeleted = false;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        response.SetSuccess(new UserNotificationPreferenceDto(existing.Id, existing.UserId, existing.Channel, existing.Category, existing.Mode, existing.CreatedAt, existing.UpdatedAt, existing.IsDeleted, existing.DeletedAt, existing.DeletedByUserId));
        return response;
    }

    public async Task<UserNotificationPreferenceResponse> DeleteUserPreferenceAsync(NotificationChannel channel, NotificationCategory category, CancellationToken cancellationToken = default)
    {
        var response = new UserNotificationPreferenceResponse();
        if (_currentUserContext.UserId is null)
        {
            response.AddResponseMessage(MessageType.ERROR, "User context is required.");
            return response;
        }

        var userId = _currentUserContext.UserId.Value;

        var existing = await _dbContext.UserNotificationPreferences
            .SingleOrDefaultAsync(p => p.UserId == userId && p.Channel == channel && p.Category == category, cancellationToken);

        if (existing is null)
        {
            response.AddResponseMessage(MessageType.WARNING, "User notification preference not found.");
            return response;
        }

        existing.IsDeleted = true;
        await _dbContext.SaveChangesAsync(cancellationToken);
        response.SetSuccess(new UserNotificationPreferenceDto(existing.Id, existing.UserId, existing.Channel, existing.Category, existing.Mode, existing.CreatedAt, existing.UpdatedAt, existing.IsDeleted, existing.DeletedAt, existing.DeletedByUserId));
        return response;
    }


    public async Task<UserPreferenceSettingsResponse> GetUserPreferenceSettingsAsync(CancellationToken cancellationToken = default)
    {
        var response = new UserPreferenceSettingsResponse();
        if (_currentUserContext.UserId is null)
        {
            response.AddResponseMessage(MessageType.ERROR, "User context is required.");
            return response;
        }

        var userId = _currentUserContext.UserId.Value;

        var user = await _dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            response.AddResponseMessage(MessageType.ERROR, "User not found.");
            return response;
        }

        response.SetSuccess(new UserPreferenceSettingsDto(user.Id, user.Id, user.PreferredEmail, user.PreferredPhoneNumber, user.CreatedAt, user.UpdatedAt, user.IsDeleted, user.DeletedAt, user.DeletedByUserId));
        return response;
    }

    public async Task<UserPreferenceSettingsResponse> UpsertUserPreferenceSettingsAsync(UpsertUserPreferenceSettingsRequest request, CancellationToken cancellationToken = default)
    {
        var response = new UserPreferenceSettingsResponse();
        if (!ServiceGuards.RequireRequest(request, "upsert user preference settings", response))
            return response;
        if (_currentUserContext.UserId is null)
        {
            response.AddResponseMessage(MessageType.ERROR, "User context is required.");
            return response;
        }

        var userId = _currentUserContext.UserId.Value;
        var preferredEmail = request.PreferredEmail?.Trim();
        var preferredPhoneNumber = request.PreferredPhoneNumber?.Trim();

        var user = await _dbContext.Users
            .SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            response.AddResponseMessage(MessageType.ERROR, "User not found.");
            return response;
        }

        user.PreferredEmail = string.IsNullOrWhiteSpace(preferredEmail) ? null : preferredEmail;
        user.PreferredPhoneNumber = string.IsNullOrWhiteSpace(preferredPhoneNumber) ? null : preferredPhoneNumber;

        await _dbContext.SaveChangesAsync(cancellationToken);
        response.SetSuccess(new UserPreferenceSettingsDto(user.Id, user.Id, user.PreferredEmail, user.PreferredPhoneNumber, user.CreatedAt, user.UpdatedAt, user.IsDeleted, user.DeletedAt, user.DeletedByUserId));
        return response;
    }

    private static MemberPreferenceSettingsDto Map(MemberPreferenceSettings settings) =>
        new(settings.Id, settings.TenantId, settings.HouseholdMemberId, settings.PreferredLanguage, settings.TimeZone, settings.CreatedAt, settings.UpdatedAt, settings.IsDeleted, settings.DeletedAt, settings.DeletedByUserId);

    private static NotificationPreferenceDto Map(TenantNotificationPolicy entity) =>
        new(entity.Id, entity.TenantId, null, entity.Channel, entity.Category, entity.Mode, entity.CreatedAt, entity.UpdatedAt, entity.IsDeleted, entity.DeletedAt, entity.DeletedByUserId);

    private static NotificationPreferenceDto Map(MemberNotificationPreference entity) =>
        new(entity.Id, entity.TenantId, entity.HouseholdMemberId, entity.Channel, entity.Category, entity.Mode, entity.CreatedAt, entity.UpdatedAt, entity.IsDeleted, entity.DeletedAt, entity.DeletedByUserId);

    private static bool IsPlausibleIanaTimeZone(string timeZone) =>
        !string.IsNullOrWhiteSpace(timeZone)
        && timeZone.Length <= 100
        && timeZone.Contains('/', StringComparison.Ordinal)
        && !timeZone.Contains(' ', StringComparison.Ordinal);
}
