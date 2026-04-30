using Gatherstead.Api.Contracts.Preferences;
using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Services.Preferences;

public interface IPreferenceService
{
    Task<MemberPreferenceSettingsResponse> GetMemberSettingsAsync(Guid tenantId, Guid householdId, Guid memberId, CancellationToken cancellationToken = default);
    Task<MemberPreferenceSettingsResponse> UpsertMemberSettingsAsync(Guid tenantId, Guid householdId, Guid memberId, UpsertMemberPreferenceSettingsRequest request, CancellationToken cancellationToken = default);

    Task<BaseEntityResponse<IReadOnlyCollection<NotificationPreferenceDto>>> ListTenantDefaultsAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<NotificationPreferenceResponse> UpsertTenantDefaultAsync(Guid tenantId, UpsertNotificationPreferenceRequest request, CancellationToken cancellationToken = default);
    Task<NotificationPreferenceResponse> DeleteTenantDefaultAsync(Guid tenantId, NotificationChannel channel, NotificationCategory category, CancellationToken cancellationToken = default);

    Task<BaseEntityResponse<IReadOnlyCollection<NotificationPreferenceDto>>> ListMemberOverridesAsync(Guid tenantId, Guid householdId, Guid memberId, CancellationToken cancellationToken = default);
    Task<NotificationPreferenceResponse> UpsertMemberOverrideAsync(Guid tenantId, Guid householdId, Guid memberId, UpsertNotificationPreferenceRequest request, CancellationToken cancellationToken = default);
    Task<NotificationPreferenceResponse> DeleteMemberOverrideAsync(Guid tenantId, Guid householdId, Guid memberId, NotificationChannel channel, NotificationCategory category, CancellationToken cancellationToken = default);

    Task<BaseEntityResponse<IReadOnlyCollection<EffectiveNotificationPreferenceDto>>> ListEffectivePreferencesAsync(Guid tenantId, Guid householdId, Guid memberId, CancellationToken cancellationToken = default);

    Task<BaseEntityResponse<IReadOnlyCollection<UserNotificationPreferenceDto>>> ListUserPreferencesAsync(CancellationToken cancellationToken = default);
    Task<UserNotificationPreferenceResponse> UpsertUserPreferenceAsync(UpsertNotificationPreferenceRequest request, CancellationToken cancellationToken = default);
    Task<UserNotificationPreferenceResponse> DeleteUserPreferenceAsync(NotificationChannel channel, NotificationCategory category, CancellationToken cancellationToken = default);
}
