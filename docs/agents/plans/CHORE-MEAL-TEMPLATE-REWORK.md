# Plan: ChoreTemplate/MealTemplate Rework + Auto-generation + Exception Tracking

## Context

Users configure an Event with StartDate/EndDate, then define ChoreTemplates and (new) MealTemplates that describe repeating tasks across the event duration. The current ChoreTemplate is limited to a single TimeSlot, ChoreTask has inconsistent naming vs. MealPlan, MealPlan has no template concept and is a flat child of Event instead of a child of a template, and there is no logic to automatically create or remove plan records when event dates change. Exception tracking is also absent. The DB has not yet been deployed, so no migration scripts are needed.

The intended ownership hierarchy:
- **Meals**: `Event` → `MealTemplate` → `MealPlan` → `MealIntent`
- **Chores**: `Event` → `ChoreTemplate` → `ChorePlan` → `ChoreIntent`

---

## 1. Enum Changes — `src/Gatherstead.Data/Entities/Enums.cs`

Add `ChoreTimeSlotFlags` (flags enum for multi-slot configuration on `ChoreTemplate`) and `MealTypeFlags` (flags enum for `MealTemplate`). Keep the original `ChoreTimeSlot` intact — it stays on `ChorePlan` (one concrete slot per plan).

```csharp
[Flags]
public enum ChoreTimeSlotFlags
{
    Morning = 0x01,
    Midday  = 0x02,
    Evening = 0x04,
    Anytime = 0x08   // standalone only; service enforces it cannot be OR'd with others
}

[Flags]
public enum MealTypeFlags
{
    Breakfast = 0x01,
    Lunch     = 0x02,
    Dinner    = 0x04
}
```

---

## 2. Rename `ChoreTask` → `ChorePlan`

**Delete:** `src/Gatherstead.Data/Entities/ChoreTask.cs`
**Create:** `src/Gatherstead.Data/Entities/ChorePlan.cs`

Hierarchy: `ChorePlan` belongs to `ChoreTemplate` (which belongs to `Event`). The `TemplateId` FK is already the only parent link — no `EventId` needed.

```csharp
[Index(nameof(TenantId), nameof(TemplateId), nameof(Completed))]
[Index(nameof(TenantId), nameof(TemplateId), nameof(Day), nameof(TimeSlot), IsUnique = true)]
public class ChorePlan : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    [ForeignKey(nameof(TenantId))]
    public Tenant? Tenant { get; set; }
    public Guid TemplateId { get; set; }
    [ForeignKey(nameof(TemplateId))]
    public ChoreTemplate? Template { get; set; }
    public DateOnly Day { get; set; }
    public ChoreTimeSlot? TimeSlot { get; set; }
    public bool Completed { get; set; }
    public string? Notes { get; set; }
    public bool IsException { get; set; }
    public string? ExceptionReason { get; set; }
    public ICollection<ChoreIntent> Intents { get; set; } = new List<ChoreIntent>();
}
```

---

## 3. Rename `ChoreAssignment` → `ChoreIntent`

**Delete:** `src/Gatherstead.Data/Entities/ChoreAssignment.cs`
**Create:** `src/Gatherstead.Data/Entities/ChoreIntent.cs`

Hierarchy: `ChoreIntent` belongs to `ChorePlan`. Consistent with `MealIntent` / `StayIntent` pattern.

```csharp
[Index(nameof(TenantId), nameof(HouseholdMemberId))]
[Index(nameof(TenantId), nameof(ChorePlanId), nameof(HouseholdMemberId), IsUnique = true)]
public class ChoreIntent : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    [ForeignKey(nameof(TenantId))]
    public Tenant? Tenant { get; set; }
    public Guid ChorePlanId { get; set; }
    [ForeignKey(nameof(ChorePlanId))]
    public ChorePlan? ChorePlan { get; set; }
    public Guid HouseholdMemberId { get; set; }
    [ForeignKey(nameof(HouseholdMemberId))]
    public HouseholdMember? HouseholdMember { get; set; }
    public bool Volunteered { get; set; }
}
```

---

## 4. Update `ChoreTemplate` — `src/Gatherstead.Data/Entities/ChoreTemplate.cs`

- `ChoreTimeSlot TimeSlot` → `ChoreTimeSlotFlags TimeSlots`
- Navigation `ICollection<ChoreTask> Tasks` → `ICollection<ChorePlan> Plans`
- Unique index: `(TenantId, EventId, Name, TimeSlot)` → `(TenantId, EventId, Name)`

---

## 5. New `MealTemplate` — `src/Gatherstead.Data/Entities/MealTemplate.cs`

Hierarchy: `MealTemplate` belongs to `Event`.

```csharp
[Index(nameof(TenantId), nameof(EventId))]
[Index(nameof(TenantId), nameof(EventId), nameof(Name), IsUnique = true)]
public class MealTemplate : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    [ForeignKey(nameof(TenantId))]
    public Tenant? Tenant { get; set; }
    public Guid EventId { get; set; }
    [ForeignKey(nameof(EventId))]
    public Event? Event { get; set; }
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    public MealTypeFlags MealTypes { get; set; }
    public string? Notes { get; set; }
    public ICollection<MealPlan> Plans { get; set; } = new List<MealPlan>();
}
```

---

## 6. Rework `MealPlan` — `src/Gatherstead.Data/Entities/MealPlan.cs`

**Key change**: `MealPlan` now belongs to `MealTemplate`, not directly to `Event`. Remove `EventId`; make `MealTemplateId` the sole (non-nullable) parent FK. Access to the owning event is via `MealTemplate.EventId`.

Unique index changes from `(TenantId, EventId, Day, MealType)` to `(TenantId, MealTemplateId, Day, MealType)`.

Add exception tracking fields.

```csharp
[Index(nameof(TenantId), nameof(MealTemplateId))]
[Index(nameof(TenantId), nameof(MealTemplateId), nameof(Day), nameof(MealType), IsUnique = true)]
public class MealPlan : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    [ForeignKey(nameof(TenantId))]
    public Tenant? Tenant { get; set; }
    public Guid MealTemplateId { get; set; }
    [ForeignKey(nameof(MealTemplateId))]
    public MealTemplate? MealTemplate { get; set; }
    public DateOnly Day { get; set; }
    public MealType MealType { get; set; }
    public string? Notes { get; set; }
    public bool IsException { get; set; }
    public string? ExceptionReason { get; set; }
    public ICollection<MealIntent> Intents { get; set; } = new List<MealIntent>();
}
```

---

## 7. Update `MealIntent` — `src/Gatherstead.Data/Entities/MealIntent.cs`

No structural changes needed — `MealIntent` already belongs to `MealPlan` via `MealPlanId`. Hierarchy is now correctly: `MealIntent` → `MealPlan` → `MealTemplate` → `Event`. No FK changes required.

---

## 8. Update `Event` — `src/Gatherstead.Data/Entities/Event.cs`

- **Remove** `ICollection<MealPlan> MealPlans` (MealPlan no longer has a direct EventId FK)
- **Add** `ICollection<MealTemplate> MealTemplates { get; set; } = new List<MealTemplate>();`

`ICollection<ChoreTemplate> ChoreTemplates` stays as-is.

---

## 9. Update `GathersteadDbContext` — `src/Gatherstead.Data/GathersteadDbContext.cs`

```csharp
// Replace:
public DbSet<ChoreTask> ChoreTasks => Set<ChoreTask>();
public DbSet<ChoreAssignment> ChoreAssignments => Set<ChoreAssignment>();
// With:
public DbSet<ChorePlan> ChorePlans => Set<ChorePlan>();
public DbSet<ChoreIntent> ChoreIntents => Set<ChoreIntent>();

// Add:
public DbSet<MealTemplate> MealTemplates => Set<MealTemplate>();
```

The existing `DeleteBehavior.Restrict` loop in `OnModelCreating` covers all new FKs automatically.

---

## 10. EventService (generation / pruning logic)

**New files:**
- `src/Gatherstead.Api/Services/Events/IEventService.cs`
- `src/Gatherstead.Api/Services/Events/EventService.cs`

Follows the same pattern as `HouseholdService`. Key logic:

### `UpdateEventDatesAsync` triggers `SyncPlansAsync`:
```
addedDays   = newRange.Except(oldRange)
removedDays = oldRange.Except(newRange)

GenerateChorePlansAsync(addedDays)   — per ChoreTemplate × ExpandSlots(template.TimeSlots)
GenerateMealPlansAsync(addedDays)    — per MealTemplate × ExpandMealTypes(template.MealTypes)
PruneChorePlansAsync(removedDays)    — safe prune: !Completed && !IsException && no Intents
PruneMealPlansAsync(removedDays)     — safe prune: !IsException && no Intents
```

### Generation idempotency:
Generation queries that need to see soft-deleted records use the `_includeDeleted` toggle on `GathersteadDbContext` (consistent with the composable filter pattern in `ARCHITECTURE.md` — never use `IgnoreQueryFilters()` as it would strip tenant isolation):
- Soft-deleted + `IsException = true` → **skip** (suppression marker — user explicitly excluded this day/slot)
- Soft-deleted + `IsException = false` → **restore** (un-delete — day was pruned when dates shrank, now restored)
- Active record exists → **leave it alone**
- No record → **insert new ChorePlan/MealPlan**

### Suppression markers (pre-emptive exceptions):
A soft-deleted `ChorePlan`/`MealPlan` with `IsException = true` and `IsDeleted = true` acts as a tombstone. Generation skips that combination, handling "don't generate first breakfast" without a separate entity.

### Helper methods:
```csharp
private static IEnumerable<ChoreTimeSlot> ExpandSlots(ChoreTimeSlotFlags flags)
{
    if (flags.HasFlag(ChoreTimeSlotFlags.Anytime)) return [ChoreTimeSlot.Anytime];
    var result = new List<ChoreTimeSlot>();
    if (flags.HasFlag(ChoreTimeSlotFlags.Morning)) result.Add(ChoreTimeSlot.Morning);
    if (flags.HasFlag(ChoreTimeSlotFlags.Midday))  result.Add(ChoreTimeSlot.Midday);
    if (flags.HasFlag(ChoreTimeSlotFlags.Evening)) result.Add(ChoreTimeSlot.Evening);
    return result;
}

private static IEnumerable<MealType> ExpandMealTypes(MealTypeFlags flags) { ... }

private static IEnumerable<DateOnly> GetDateRange(DateOnly start, DateOnly end) { ... }
```

---

## 11. Plan Generation Utilities — `src/Gatherstead.Data/Planning/PlanGenerator.cs`

A static utility class in `Gatherstead.Data` (accessible to both the API and any maintenance tooling) that encapsulates all pure plan-generation logic with no DbContext dependency. `EventService` becomes a thin orchestrator: load data, call `PlanGenerator`, apply the diff.

### Result types

```csharp
public record ChorePlanDiff
{
    // New slots to insert as ChorePlan records
    public IReadOnlyList<(DateOnly Day, ChoreTimeSlot Slot)> ToAdd { get; init; } = [];
    // Soft-deleted (!IsException) ChorePlans to restore (un-delete)
    public IReadOnlyList<ChorePlan> ToRestore { get; init; } = [];
    // Active (!Completed, !IsException, no Intents) ChorePlans to soft-delete
    public IReadOnlyList<ChorePlan> ToPrune { get; init; } = [];
}

public record MealPlanDiff
{
    public IReadOnlyList<(DateOnly Day, MealType MealType)> ToAdd { get; init; } = [];
    public IReadOnlyList<MealPlan> ToRestore { get; init; } = [];
    public IReadOnlyList<MealPlan> ToPrune { get; init; } = [];
}
```

### `PlanGenerator` API

```csharp
public static class PlanGenerator
{
    // Pure diff: given a template's slot config and a date range, compare against existing plans
    // (existingPlans must include soft-deleted records for suppression-marker detection)
    public static ChorePlanDiff DiffChorePlans(
        ChoreTimeSlotFlags timeSlots,
        DateOnly start,
        DateOnly end,
        IEnumerable<ChorePlan> existingPlans);

    public static MealPlanDiff DiffMealPlans(
        MealTypeFlags mealTypes,
        DateOnly start,
        DateOnly end,
        IEnumerable<MealPlan> existingPlans);

    // Expand a flags value into individual enum values
    public static IEnumerable<ChoreTimeSlot> ExpandSlots(ChoreTimeSlotFlags flags);
    public static IEnumerable<MealType> ExpandMealTypes(MealTypeFlags flags);
    public static IEnumerable<DateOnly> GetDateRange(DateOnly start, DateOnly end);
}
```

### Diff logic (inside `DiffChorePlans`):

```
expected = GetDateRange(start, end) × ExpandSlots(timeSlots)

for each (day, slot) in expected:
    match = existingPlans.FirstOrDefault(p => p.Day == day && p.TimeSlot == slot)
    if match is null                          → ToAdd
    if match.IsDeleted && match.IsException   → skip (suppression marker, honour it)
    if match.IsDeleted && !match.IsException  → ToRestore

for each active existing plan NOT in expected:
    if !Completed && !IsException && no Intents → ToPrune
    otherwise                                   → leave alone (user data, do not prune)
```

### DB maintenance use case

When a `ChoreTemplate.TimeSlots` or `MealTemplate.MealTypes` is updated (not just event date changes), the same utilities regenerate plans for the full event range:

```csharp
// In a future ChoreTemplateService or maintenance job:
var diff = PlanGenerator.DiffChorePlans(
    updatedTemplate.TimeSlots,
    @event.StartDate, @event.EndDate,
    existingPlans  // all plans for this template, including soft-deleted
);
// Apply diff via DbContext
```

### Unit test surface

Because `PlanGenerator` is pure (no DbContext, no DI), tests can run entirely in-memory:

```csharp
// Example test cases:
// - 3-day event, Morning|Evening → 6 ChorePlans produced
// - Suppress day 2 Evening via tombstone → 5 ChorePlans, tombstone slot skipped
// - Extend event by 1 day → only the 2 new-day slots in ToAdd, existing untouched
// - Shorten event by 1 day → last-day plans with no data in ToPrune; completed plan stays
// - Change template from Morning to Morning|Midday → Midday slots for all days in ToAdd
```

---

## 12. Docs Updates

### `docs/ARCHITECTURE.md` — Gathering Planning Context section

Replace `MealPlan`, `MealIntent`, `ChoreTemplate`, `ChoreTask` bullets with:

```
- **MealTemplate**: Template scoped to an event specifying which meal types (Breakfast/Lunch/Dinner via MealTypeFlags) to generate across the event's date range.
- **MealPlan**: A specific meal on a specific day, owned by a MealTemplate. Supports exception marking to suppress auto-generated entries. Aggregates meal intents.
- **MealIntent**: Member-level response indicating attendance for a meal, dietary considerations, and bring-your-own-food choices.
- **ChoreTemplate**: Template for recurring chores across an event; specifies one or more time slots (Morning/Midday/Evening/Anytime via ChoreTimeSlotFlags) and drives automatic ChorePlan generation.
- **ChorePlan**: Dated chore instance for a specific day and time slot, owned by a ChoreTemplate. Supports exception marking and completion tracking. (Renamed from ChoreTask.)
- **ChoreIntent**: Member's volunteer/assignment record for a ChorePlan. (Renamed from ChoreAssignment; consistent with MealIntent/StayIntent pattern.)
```

### `docs/IMPLEMENTATION_STATUS.md`

- In **Implemented Features**: update description of gathering planning entities to use `ChorePlan`, `ChoreIntent`, `MealTemplate`; remove `ChoreTask`/`ChoreAssignment` references.
- In **Planned Enhancements**:
  - Update "Chore sign-up flows" to reference `ChoreIntent` and `ChorePlan`
  - Update "Gathering Planning API" to reference `MealTemplate`, exception tracking, and hierarchical meal/chore ownership
  - Add: **Event plan auto-generation**: When an event's date range changes, `ChorePlan` and `MealPlan` records are automatically generated for added days (per template configuration) or safely pruned for removed days (only if no user data — intents, completion — and `IsException = false`).

---

## 12. Files Changed / Created

| Action | Path |
|--------|------|
| Modify | `src/Gatherstead.Data/Entities/Enums.cs` |
| Rename+modify | `src/Gatherstead.Data/Entities/ChoreTask.cs` → `ChorePlan.cs` |
| Rename+modify | `src/Gatherstead.Data/Entities/ChoreAssignment.cs` → `ChoreIntent.cs` |
| Modify | `src/Gatherstead.Data/Entities/ChoreTemplate.cs` |
| Rework | `src/Gatherstead.Data/Entities/MealPlan.cs` |
| Modify | `src/Gatherstead.Data/Entities/Event.cs` |
| Modify | `src/Gatherstead.Data/GathersteadDbContext.cs` |
| Create | `src/Gatherstead.Data/Entities/MealTemplate.cs` |
| Create | `src/Gatherstead.Data/Planning/PlanGenerator.cs` |
| Create | `src/Gatherstead.Api/Services/Events/IEventService.cs` |
| Create | `src/Gatherstead.Api/Services/Events/EventService.cs` |
| Modify | `docs/ARCHITECTURE.md` |
| Modify | `docs/IMPLEMENTATION_STATUS.md` |

Controllers/contracts for Events are out of scope for this iteration.

---

## 13. Verification

1. `dotnet build` — no compile errors
2. Unit tests for `PlanGenerator` (pure, no DbContext):
   - `ExpandSlots` / `ExpandMealTypes` cover all flag combinations including `Anytime` sentinel
   - 3-day event, `Morning|Evening` → 6 ChorePlan slots in `ToAdd`
   - Suppression marker present → that slot absent from `ToAdd`
   - Extend date range → only new-day slots in `ToAdd`, existing untouched
   - Shorten date range → no-data plans in `ToPrune`; completed/exception/intent-bearing plans omitted from `ToPrune`
   - Template slot change (`Morning` → `Morning|Midday`) → `Midday` slots for all existing days in `ToAdd`
3. Integration test: create Event with MealTemplate (Breakfast|Dinner) + ChoreTemplate (Morning|Evening); confirm generation produces correct ChorePlan and MealPlan rows
4. Integration test: extend event EndDate → new plans generated for added days
5. Integration test: shorten event EndDate → only unworked, intent-free, non-exception plans pruned
6. Integration test: create suppression marker for day+slot → generation skips that combination
7. Confirm `docs/ARCHITECTURE.md` entity descriptions match updated class/property names
