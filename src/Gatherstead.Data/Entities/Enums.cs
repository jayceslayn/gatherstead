namespace Gatherstead.Data.Entities;

public enum TenantRole
{
    Owner,
    Manager,
    Member,
    Guest
}

public enum ResourceType
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

public enum MealIntentStatus
{
    Going,
    Maybe,
    NotGoing
}

public enum StayIntentStatus
{
    Intent,
    Hold,
    Confirmed
}

public enum StayIntentDecision
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
