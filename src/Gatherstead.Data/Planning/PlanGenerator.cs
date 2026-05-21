using System;
using System.Collections.Generic;
using System.Linq;
using Gatherstead.Data.Entities;

namespace Gatherstead.Data.Planning;

public record TaskPlanDiff
{
    public IReadOnlyList<(DateOnly Day, TaskTimeSlot Slot)> ToAdd { get; init; } = [];
    public IReadOnlyList<TaskPlan> ToRestore { get; init; } = [];
    public IReadOnlyList<TaskPlan> ToPrune { get; init; } = [];
}

public record MealPlanDiff
{
    public IReadOnlyList<(DateOnly Day, MealType MealType)> ToAdd { get; init; } = [];
    public IReadOnlyList<MealPlan> ToRestore { get; init; } = [];
    public IReadOnlyList<MealPlan> ToPrune { get; init; } = [];
}

public static class PlanGenerator
{
    /// <summary>
    /// Computes which TaskPlan records need to be added, restored, or pruned to align the
    /// existing collection with the template's slot configuration over the given date range.
    /// <paramref name="existingPlans"/> must include soft-deleted records so suppression
    /// markers (IsDeleted &amp;&amp; IsException) are honoured during generation.
    /// </summary>
    public static TaskPlanDiff DiffTaskPlans(
        TaskTimeSlotFlags timeSlots,
        DateOnly start,
        DateOnly end,
        IEnumerable<TaskPlan> existingPlans,
        DateOnly? startDate = null,
        DateOnly? endDate = null)
    {
        var effectiveStart = startDate ?? start;
        var effectiveEnd   = endDate   ?? end;

        var slots = ExpandSlots(timeSlots).ToList();
        var existing = existingPlans.ToList();

        var toAdd = new List<(DateOnly, TaskTimeSlot)>();
        var toRestore = new List<TaskPlan>();

        foreach (var day in GetDateRange(effectiveStart, effectiveEnd))
        {
            foreach (var slot in slots)
            {
                var match = existing.FirstOrDefault(p => p.Day == day && p.TimeSlot == slot);
                if (match is null)
                {
                    toAdd.Add((day, slot));
                }
                else if (match.IsDeleted && match.IsException)
                {
                    // suppression marker — honour it, skip generation
                }
                else if (match.IsDeleted)
                {
                    toRestore.Add(match);
                }
                // else active record — leave it alone
            }
        }

        var expectedKeys = GetDateRange(effectiveStart, effectiveEnd)
            .SelectMany(day => slots.Select(slot => (day, slot)))
            .ToHashSet();

        var toPrune = existing
            .Where(p =>
                !p.IsDeleted &&
                !p.IsException &&
                !p.Completed &&
                !p.Intents.Any() &&
                p.TimeSlot.HasValue &&
                !expectedKeys.Contains((p.Day, p.TimeSlot!.Value)))
            .ToList();

        return new TaskPlanDiff
        {
            ToAdd = toAdd,
            ToRestore = toRestore,
            ToPrune = toPrune,
        };
    }

    /// <summary>
    /// Computes which MealPlan records need to be added, restored, or pruned to align the
    /// existing collection with the template's meal type configuration over the given date range.
    /// <paramref name="existingPlans"/> must include soft-deleted records so suppression
    /// markers (IsDeleted &amp;&amp; IsException) are honoured during generation.
    /// </summary>
    public static MealPlanDiff DiffMealPlans(
        MealTypeFlags mealTypes,
        DateOnly start,
        DateOnly end,
        IEnumerable<MealPlan> existingPlans,
        DateOnly? startDate = null,
        DateOnly? endDate = null)
    {
        var effectiveStart = startDate ?? start;
        var effectiveEnd   = endDate   ?? end;

        var types = ExpandMealTypes(mealTypes).ToList();
        var existing = existingPlans.ToList();

        var toAdd = new List<(DateOnly, MealType)>();
        var toRestore = new List<MealPlan>();

        foreach (var day in GetDateRange(effectiveStart, effectiveEnd))
        {
            foreach (var mealType in types)
            {
                var match = existing.FirstOrDefault(p => p.Day == day && p.MealType == mealType);
                if (match is null)
                {
                    toAdd.Add((day, mealType));
                }
                else if (match.IsDeleted && match.IsException)
                {
                    // suppression marker — honour it, skip generation
                }
                else if (match.IsDeleted)
                {
                    toRestore.Add(match);
                }
                // else active record — leave it alone
            }
        }

        var expectedKeys = GetDateRange(effectiveStart, effectiveEnd)
            .SelectMany(day => types.Select(t => (day, t)))
            .ToHashSet();

        var toPrune = existing
            .Where(p =>
                !p.IsDeleted &&
                !p.IsException &&
                !p.Intents.Any() &&
                !expectedKeys.Contains((p.Day, p.MealType)))
            .ToList();

        return new MealPlanDiff
        {
            ToAdd = toAdd,
            ToRestore = toRestore,
            ToPrune = toPrune,
        };
    }

    /// <summary>
    /// Expands a <see cref="TaskTimeSlotFlags"/> bitmask into individual <see cref="TaskTimeSlot"/> values.
    /// If <see cref="TaskTimeSlotFlags.Anytime"/> is set it is returned alone; it cannot be combined.
    /// </summary>
    public static IEnumerable<TaskTimeSlot> ExpandSlots(TaskTimeSlotFlags flags)
    {
        if (flags.HasFlag(TaskTimeSlotFlags.Anytime))
        {
            yield return TaskTimeSlot.Anytime;
            yield break;
        }

        if (flags.HasFlag(TaskTimeSlotFlags.Morning)) yield return TaskTimeSlot.Morning;
        if (flags.HasFlag(TaskTimeSlotFlags.Midday))  yield return TaskTimeSlot.Midday;
        if (flags.HasFlag(TaskTimeSlotFlags.Evening)) yield return TaskTimeSlot.Evening;
    }

    /// <summary>
    /// Expands a <see cref="MealTypeFlags"/> bitmask into individual <see cref="MealType"/> values.
    /// </summary>
    public static IEnumerable<MealType> ExpandMealTypes(MealTypeFlags flags)
    {
        if (flags.HasFlag(MealTypeFlags.Breakfast)) yield return MealType.Breakfast;
        if (flags.HasFlag(MealTypeFlags.Lunch))     yield return MealType.Lunch;
        if (flags.HasFlag(MealTypeFlags.Dinner))    yield return MealType.Dinner;
    }

    public static IEnumerable<DateOnly> GetDateRange(DateOnly start, DateOnly end)
    {
        for (var d = start; d <= end; d = d.AddDays(1))
            yield return d;
    }
}
