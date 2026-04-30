using System.ComponentModel.DataAnnotations;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Contracts.Preferences;

public record MemberPreferenceSettingsDto(
    Guid Id,
    Guid TenantId,
    Guid HouseholdMemberId,
    string PreferredLanguage,
    string TimeZone,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool IsDeleted,
    DateTimeOffset? DeletedAt,
    Guid? DeletedByUserId);

public record NotificationPreferenceDto(
    Guid Id,
    Guid TenantId,
    Guid? HouseholdMemberId,
    NotificationChannel Channel,
    NotificationCategory Category,
    NotificationMode Mode,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool IsDeleted,
    DateTimeOffset? DeletedAt,
    Guid? DeletedByUserId);

public record EffectiveNotificationPreferenceDto(
    NotificationChannel Channel,
    NotificationCategory Category,
    NotificationMode Mode,
    string Source);


public record UserNotificationPreferenceDto(
    Guid Id,
    Guid UserId,
    NotificationChannel Channel,
    NotificationCategory Category,
    NotificationMode Mode,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool IsDeleted,
    DateTimeOffset? DeletedAt,
    Guid? DeletedByUserId);

public class UserNotificationPreferenceResponse : BaseEntityResponse<UserNotificationPreferenceDto> { }


public record UserPreferenceSettingsDto(
    Guid Id,
    Guid UserId,
    string? PreferredEmail,
    string? PreferredPhoneNumber,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool IsDeleted,
    DateTimeOffset? DeletedAt,
    Guid? DeletedByUserId);

public class UserPreferenceSettingsResponse : BaseEntityResponse<UserPreferenceSettingsDto> { }

public class UpsertUserPreferenceSettingsRequest
{
    [EmailAddress]
    [StringLength(320)]
    public string? PreferredEmail { get; init; }

    [Phone]
    [StringLength(32)]
    public string? PreferredPhoneNumber { get; init; }
}

public class MemberPreferenceSettingsResponse : BaseEntityResponse<MemberPreferenceSettingsDto> { }
public class NotificationPreferenceResponse : BaseEntityResponse<NotificationPreferenceDto> { }

public class UpsertMemberPreferenceSettingsRequest
{
    [Required]
    [StringLength(35)]
    public string PreferredLanguage { get; init; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string TimeZone { get; init; } = string.Empty;
}

public class UpsertNotificationPreferenceRequest
{
    [Required]
    public NotificationChannel Channel { get; init; }

    [Required]
    public NotificationCategory Category { get; init; }

    [Required]
    public NotificationMode Mode { get; init; }
}
