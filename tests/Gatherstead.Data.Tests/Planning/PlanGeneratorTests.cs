using Gatherstead.Data.Entities;
using Gatherstead.Data.Planning;

namespace Gatherstead.Data.Tests.Planning;

public class PlanGeneratorTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid TemplateId = Guid.NewGuid();
    private static readonly Guid MealTemplateId = Guid.NewGuid();

    // ── ExpandSlots ──────────────────────────────────────────────────────────

    [Fact]
    public void ExpandSlots_SingleFlag_ReturnsSingleSlot()
    {
        Assert.Equal([TaskTimeSlot.Morning], PlanGenerator.ExpandSlots(TaskTimeSlotFlags.Morning));
        Assert.Equal([TaskTimeSlot.Midday], PlanGenerator.ExpandSlots(TaskTimeSlotFlags.Midday));
        Assert.Equal([TaskTimeSlot.Evening], PlanGenerator.ExpandSlots(TaskTimeSlotFlags.Evening));
    }

    [Fact]
    public void ExpandSlots_MultipleFlags_ReturnsAllSlots()
    {
        var slots = PlanGenerator.ExpandSlots(TaskTimeSlotFlags.Morning | TaskTimeSlotFlags.Evening).ToList();
        Assert.Equal(2, slots.Count);
        Assert.Contains(TaskTimeSlot.Morning, slots);
        Assert.Contains(TaskTimeSlot.Evening, slots);
    }

    [Fact]
    public void ExpandSlots_AllCombinableFlags_ReturnsThreeSlots()
    {
        var slots = PlanGenerator.ExpandSlots(
            TaskTimeSlotFlags.Morning | TaskTimeSlotFlags.Midday | TaskTimeSlotFlags.Evening).ToList();
        Assert.Equal(3, slots.Count);
    }

    [Fact]
    public void ExpandSlots_Anytime_ReturnsSingletonAndIgnoresOtherBits()
    {
        // Anytime is standalone; combining with other bits still returns only Anytime
        var slots = PlanGenerator.ExpandSlots(TaskTimeSlotFlags.Anytime | TaskTimeSlotFlags.Morning).ToList();
        Assert.Equal([TaskTimeSlot.Anytime], slots);
    }

    // ── ExpandMealTypes ──────────────────────────────────────────────────────

    [Fact]
    public void ExpandMealTypes_SingleFlag_ReturnsSingleType()
    {
        Assert.Equal([MealType.Breakfast], PlanGenerator.ExpandMealTypes(MealTypeFlags.Breakfast));
        Assert.Equal([MealType.Lunch], PlanGenerator.ExpandMealTypes(MealTypeFlags.Lunch));
        Assert.Equal([MealType.Dinner], PlanGenerator.ExpandMealTypes(MealTypeFlags.Dinner));
    }

    [Fact]
    public void ExpandMealTypes_AllFlags_ReturnsAllTypes()
    {
        var types = PlanGenerator.ExpandMealTypes(
            MealTypeFlags.Breakfast | MealTypeFlags.Lunch | MealTypeFlags.Dinner).ToList();
        Assert.Equal(3, types.Count);
        Assert.Contains(MealType.Breakfast, types);
        Assert.Contains(MealType.Lunch, types);
        Assert.Contains(MealType.Dinner, types);
    }

    // ── GetDateRange ─────────────────────────────────────────────────────────

    [Fact]
    public void GetDateRange_SingleDay_ReturnsOneDay()
    {
        var d = new DateOnly(2025, 7, 4);
        Assert.Equal([d], PlanGenerator.GetDateRange(d, d));
    }

    [Fact]
    public void GetDateRange_ThreeDays_ReturnsThreeDays()
    {
        var start = new DateOnly(2025, 7, 1);
        var end = new DateOnly(2025, 7, 3);
        var days = PlanGenerator.GetDateRange(start, end).ToList();
        Assert.Equal(3, days.Count);
        Assert.Equal(start, days[0]);
        Assert.Equal(end, days[2]);
    }

    // ── DiffTaskPlans ───────────────────────────────────────────────────────

    [Fact]
    public void DiffTaskPlans_NoExisting_AddsAllSlots()
    {
        var start = new DateOnly(2025, 7, 1);
        var end = new DateOnly(2025, 7, 3);
        var flags = TaskTimeSlotFlags.Morning | TaskTimeSlotFlags.Evening;

        var diff = PlanGenerator.DiffTaskPlans(flags, start, end, []);

        Assert.Equal(6, diff.ToAdd.Count);  // 3 days × 2 slots
        Assert.Empty(diff.ToRestore);
        Assert.Empty(diff.ToPrune);
    }

    [Fact]
    public void DiffTaskPlans_AllExistAndActive_NothingChanges()
    {
        var start = new DateOnly(2025, 7, 1);
        var end = new DateOnly(2025, 7, 2);
        var existing = new[]
        {
            MakeTaskPlan(start, TaskTimeSlot.Morning),
            MakeTaskPlan(start, TaskTimeSlot.Evening),
            MakeTaskPlan(end,   TaskTimeSlot.Morning),
            MakeTaskPlan(end,   TaskTimeSlot.Evening),
        };

        var diff = PlanGenerator.DiffTaskPlans(
            TaskTimeSlotFlags.Morning | TaskTimeSlotFlags.Evening, start, end, existing);

        Assert.Empty(diff.ToAdd);
        Assert.Empty(diff.ToRestore);
        Assert.Empty(diff.ToPrune);
    }

    [Fact]
    public void DiffTaskPlans_SoftDeletedNonException_IsRestored()
    {
        var day = new DateOnly(2025, 7, 1);
        var plan = MakeTaskPlan(day, TaskTimeSlot.Morning, isDeleted: true);

        var diff = PlanGenerator.DiffTaskPlans(TaskTimeSlotFlags.Morning, day, day, [plan]);

        Assert.Empty(diff.ToAdd);
        Assert.Single(diff.ToRestore);
        Assert.Empty(diff.ToPrune);
    }

    [Fact]
    public void DiffTaskPlans_SuppressionMarker_IsSkipped()
    {
        var day = new DateOnly(2025, 7, 1);
        var tombstone = MakeTaskPlan(day, TaskTimeSlot.Morning, isDeleted: true, isException: true);

        var diff = PlanGenerator.DiffTaskPlans(TaskTimeSlotFlags.Morning, day, day, [tombstone]);

        // Neither added nor restored — suppression marker is honoured
        Assert.Empty(diff.ToAdd);
        Assert.Empty(diff.ToRestore);
        Assert.Empty(diff.ToPrune);
    }

    [Fact]
    public void DiffTaskPlans_DayRemovedFromRange_PrunesEmptyPlan()
    {
        var inRange = new DateOnly(2025, 7, 1);
        var removed = new DateOnly(2025, 7, 2);
        var existing = new[]
        {
            MakeTaskPlan(inRange, TaskTimeSlot.Morning),
            MakeTaskPlan(removed, TaskTimeSlot.Morning),
        };

        var diff = PlanGenerator.DiffTaskPlans(TaskTimeSlotFlags.Morning, inRange, inRange, existing);

        Assert.Empty(diff.ToAdd);
        Assert.Empty(diff.ToRestore);
        Assert.Single(diff.ToPrune);
        Assert.Equal(removed, diff.ToPrune[0].Day);
    }

    [Fact]
    public void DiffTaskPlans_CompletedPlanOutOfRange_NotPruned()
    {
        var inRange = new DateOnly(2025, 7, 1);
        var removed = new DateOnly(2025, 7, 2);
        var completedPlan = MakeTaskPlan(removed, TaskTimeSlot.Morning, completed: true);

        var diff = PlanGenerator.DiffTaskPlans(TaskTimeSlotFlags.Morning, inRange, inRange, [completedPlan]);

        Assert.Empty(diff.ToPrune);
    }

    [Fact]
    public void DiffTaskPlans_ExceptionPlanOutOfRange_NotPruned()
    {
        var inRange = new DateOnly(2025, 7, 1);
        var removed = new DateOnly(2025, 7, 2);
        var exceptionPlan = MakeTaskPlan(removed, TaskTimeSlot.Morning, isException: true);

        var diff = PlanGenerator.DiffTaskPlans(TaskTimeSlotFlags.Morning, inRange, inRange, [exceptionPlan]);

        Assert.Empty(diff.ToPrune);
    }

    [Fact]
    public void DiffTaskPlans_PlanWithIntentsOutOfRange_NotPruned()
    {
        var inRange = new DateOnly(2025, 7, 1);
        var removed = new DateOnly(2025, 7, 2);
        var planWithIntent = MakeTaskPlan(removed, TaskTimeSlot.Morning, intentCount: 1);

        var diff = PlanGenerator.DiffTaskPlans(TaskTimeSlotFlags.Morning, inRange, inRange, [planWithIntent]);

        Assert.Empty(diff.ToPrune);
    }

    [Fact]
    public void DiffTaskPlans_TemplateSlotChange_AddsMissingSlots()
    {
        // Existing plans only have Morning; template now covers Morning|Midday
        var start = new DateOnly(2025, 7, 1);
        var end = new DateOnly(2025, 7, 2);
        var existing = new[]
        {
            MakeTaskPlan(start, TaskTimeSlot.Morning),
            MakeTaskPlan(end,   TaskTimeSlot.Morning),
        };

        var diff = PlanGenerator.DiffTaskPlans(
            TaskTimeSlotFlags.Morning | TaskTimeSlotFlags.Midday, start, end, existing);

        Assert.Equal(2, diff.ToAdd.Count);
        Assert.All(diff.ToAdd, item => Assert.Equal(TaskTimeSlot.Midday, item.Slot));
        Assert.Empty(diff.ToPrune);
    }

    // ── DiffMealPlans ────────────────────────────────────────────────────────

    [Fact]
    public void DiffMealPlans_NoExisting_AddsAllTypes()
    {
        var start = new DateOnly(2025, 7, 1);
        var end = new DateOnly(2025, 7, 3);
        var flags = MealTypeFlags.Breakfast | MealTypeFlags.Dinner;

        var diff = PlanGenerator.DiffMealPlans(flags, start, end, []);

        Assert.Equal(6, diff.ToAdd.Count);  // 3 days × 2 types
        Assert.Empty(diff.ToRestore);
        Assert.Empty(diff.ToPrune);
    }

    [Fact]
    public void DiffMealPlans_SuppressionMarker_IsSkipped()
    {
        var day = new DateOnly(2025, 7, 1);
        var tombstone = MakeMealPlan(day, MealType.Breakfast, isDeleted: true, isException: true);

        var diff = PlanGenerator.DiffMealPlans(MealTypeFlags.Breakfast, day, day, [tombstone]);

        Assert.Empty(diff.ToAdd);
        Assert.Empty(diff.ToRestore);
        Assert.Empty(diff.ToPrune);
    }

    [Fact]
    public void DiffMealPlans_SoftDeletedNonException_IsRestored()
    {
        var day = new DateOnly(2025, 7, 1);
        var plan = MakeMealPlan(day, MealType.Dinner, isDeleted: true);

        var diff = PlanGenerator.DiffMealPlans(MealTypeFlags.Dinner, day, day, [plan]);

        Assert.Single(diff.ToRestore);
        Assert.Empty(diff.ToAdd);
    }

    [Fact]
    public void DiffMealPlans_DayRemovedFromRange_PrunesEmptyPlan()
    {
        var inRange = new DateOnly(2025, 7, 1);
        var removed = new DateOnly(2025, 7, 2);
        var existing = new[]
        {
            MakeMealPlan(inRange, MealType.Breakfast),
            MakeMealPlan(removed, MealType.Breakfast),
        };

        var diff = PlanGenerator.DiffMealPlans(MealTypeFlags.Breakfast, inRange, inRange, existing);

        Assert.Single(diff.ToPrune);
        Assert.Equal(removed, diff.ToPrune[0].Day);
    }

    [Fact]
    public void DiffMealPlans_PlanWithIntentsOutOfRange_NotPruned()
    {
        var inRange = new DateOnly(2025, 7, 1);
        var removed = new DateOnly(2025, 7, 2);
        var planWithIntent = MakeMealPlan(removed, MealType.Lunch, intentCount: 1);

        var diff = PlanGenerator.DiffMealPlans(MealTypeFlags.Lunch, inRange, inRange, [planWithIntent]);

        Assert.Empty(diff.ToPrune);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static TaskPlan MakeTaskPlan(
        DateOnly day,
        TaskTimeSlot slot,
        bool isDeleted = false,
        bool isException = false,
        bool completed = false,
        int intentCount = 0) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = TenantId,
        TemplateId = TemplateId,
        Day = day,
        TimeSlot = slot,
        Completed = completed,
        IsDeleted = isDeleted,
        IsException = isException,
        Intents = Enumerable.Range(0, intentCount)
            .Select(_ => new TaskIntent { Id = Guid.NewGuid(), TenantId = TenantId })
            .ToList(),
    };

    private static MealPlan MakeMealPlan(
        DateOnly day,
        MealType mealType,
        bool isDeleted = false,
        bool isException = false,
        int intentCount = 0) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = TenantId,
        MealTemplateId = MealTemplateId,
        Day = day,
        MealType = mealType,
        IsDeleted = isDeleted,
        IsException = isException,
        Intents = Enumerable.Range(0, intentCount)
            .Select(_ => new MealIntent { Id = Guid.NewGuid(), TenantId = TenantId })
            .ToList(),
    };
}
