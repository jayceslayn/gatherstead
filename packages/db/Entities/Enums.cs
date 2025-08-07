namespace Gatherstead.Db.Entities;

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

public enum ChoreTimeSlot
{
    Morning,
    Midday,
    Evening,
    Anytime
}
