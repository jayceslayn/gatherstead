namespace Gatherstead.Data.Entities;

public enum TenantRole
{
    Owner,
    Manager,
    Member,
    Guest
}

public enum AccommodationType
{
    Bedroom,
    Bunk,
    RvPad,
    Tent,
    Offsite
}

public enum MealType
{
    Breakfast,
    Lunch,
    Dinner
}

public enum AccommodationIntentStatus
{
    Intent,
    Hold,
    Confirmed
}

public enum AccommodationIntentDecision
{
    Pending,
    Approved,
    Declined
}

public enum ChoreTimeSlot
{
    Morning,
    Midday,
    Evening,
    Anytime
}

[Flags]
public enum ChoreTimeSlotFlags
{
    Morning = 0x01,
    Midday  = 0x02,
    Evening = 0x04,
    Anytime = 0x08
}

[Flags]
public enum MealTypeFlags
{
    Breakfast = 0x01,
    Lunch = 0x02,
    Dinner = 0x04
}

public enum RelationshipType
{
    Parent,
    Child,
    Sibling,
    Spouse,
    Guardian,
    Other
}

public enum ContactMethodType
{
    Email,
    Phone,
    Other
}

public enum AttendanceStatus
{
    Going,
    Maybe,
    NotGoing
}

public enum HouseholdRole
{
    Admin,
    Member
}


public enum NotificationChannel
{
    Email,
    Sms
}

public enum NotificationCategory
{
    Invites,
    Responsibilities,
    Billing,
    System,
    Feedback
}

public enum NotificationMode
{
    Immediate,
    Digest,
    Muted
}

public enum SecurityEventType
{
    AuthFailure,
    AuthzDenial,
    CrossTenantWriteBlocked,
    TokenRevoked,
    SoftDelete,
    Restore,
    AppAdminAction,
    RateLimitBreach
}

public enum SecurityEventSeverity
{
    Info,
    Warning,
    Critical
}
