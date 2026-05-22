namespace Gatherstead.Data.Entities;

public enum TenantRole
{
    Owner = 0,
    Manager = 1,
    Coordinator = 2,
    Member = 3,
    Guest = 4
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

public enum TaskTimeSlot
{
    Morning,
    Midday,
    Evening,
    Anytime
}

[Flags]
public enum TaskTimeSlotFlags
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
    Manager = 0,
    Member = 1
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
