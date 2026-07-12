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
    Requested = 0,  // was Intent (+ former Decision.Pending)
    Hold = 1,
    Confirmed = 2,  // was Confirmed (+ former Decision.Approved)
    Declined = 3    // was former Decision.Declined
}

/// <summary>Records who initiated an intent row; the row's existence is the sign-up itself.</summary>
public enum IntentSource
{
    Volunteered = 0,  // member signed themselves up (self or own-household manager)
    Assigned = 1      // a coordinator/manager/owner/app-admin signed the member up
}

/// <summary>Bed inventory sizes for an accommodation. Implied sleeps counts live in <c>BedSizes.Sleeps</c>.</summary>
public enum BedSize
{
    Single = 0,
    Double = 1,
    Queen = 2,
    King = 3,
    Bunk = 4,
    Sofa = 5,
    Crib = 6,
    Other = 7
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

public enum InvitationStatus
{
    Pending = 0,
    Accepted = 1,
    Revoked = 2
}

public enum SecurityEventType
{
    AuthFailure = 0,
    AuthzDenial = 1,
    CrossTenantWriteBlocked = 2,
    TokenRevoked = 3,
    // 4 (SoftDelete) and 5 (Restore) were never emitted and are removed. Their numeric
    // values are reserved to keep the persisted EventType numbers stable — do not reuse.
    AppAdminAction = 6,
    RateLimitBreach = 7,
    InvitationCreated = 8,
    InvitationAccepted = 9,
    // Emitted when a user's account and personal data are hard-erased (self-service or admin).
    // Append-only and PII-free: the durable record that an erasure occurred. See AccountDeletionService.
    AccountDeleted = 10
}

public enum SecurityEventSeverity
{
    Info,
    Warning,
    Critical
}

public enum DietaryCategory
{
    Diet        = 0,
    Allergy     = 1,
    Restriction = 2,
}

public enum AgeBand
{
    Age0To2   = 0,
    Age3To5   = 1,
    Age6To12  = 2,
    Age13To17 = 3,
    Age18To64 = 4,
    Age65Plus = 5,
}

public enum ShoppingItemOrigin
{
    Property = 0,
    Event    = 1,
    Meal     = 2,
}

public enum ShoppingItemStatus
{
    Needed  = 0,
    Claimed = 1,
    Covered = 2,
}

public enum ShoppingItemIntentStatus
{
    Claimed  = 0,   // committed to bring, not yet delivered
    Provided = 1,   // actually brought / delivered
}
