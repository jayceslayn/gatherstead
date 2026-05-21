# Plan: TaskTemplate/MealTemplate Rework + Auto-generation + Exception Tracking

## Context

Users configure an Event with StartDate/EndDate, then define TaskTemplates and (new) MealTemplates that describe repeating tasks across the event duration. The current TaskTemplate is limited to a single TimeSlot, TaskTask has inconsistent naming vs. MealPlan, MealPlan has no template concept and is a flat child of Event instead of a child of a template, and there is no logic to automatically create or remove plan records when event dates change. Exception tracking is also absent. The DB has not yet been deployed, so no migration scripts are needed.

The intended ownership hierarchy:
- **Meals**: `Event` → `MealTemplate` → `MealPlan` → `MealIntent`
- **Tasks**: `Event` → `TaskTemplate` → `TaskPlan` → `TaskIntent`

---

## 1. Enum Changes — `src/Gatherstead.Data/Entities/Enums.cs`

Add `TaskTimeSlotFlags` (flags enum for multi-slot configuration on `TaskTemplate`) and `MealTypeFlags` (flags enum for `MealTemplate`). Keep the original `TaskTimeSlot` intact — it stays on `TaskPlan` (one concrete slot per plan).

```csharp
[Flags]
public enum TaskTimeSlotFlags
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

## 2. Rename `TaskTask` → `TaskPlan`

**Delete:** `src/Gatherstead.Data/Entities/TaskTask.cs`
**Create:** `src/Gatherstead.Data/Entities/TaskPlan.cs`

Hierarchy: `TaskPlan` belongs to `TaskTemplate` (which belongs to `Event`). The `TemplateId` FK is already the only parent link — no `EventId` needed.

```csharp
[Index(nameof(TenantId), nameof(TemplateId), nameof(Completed))]
[Index(nameof(TenantId), nameof(TemplateId), nameof(Day), nameof(TimeSlot), IsUnique = true)]
public class TaskPlan : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    [ForeignKey(nameof(TenantId))]
    public Tenant? Tenant { get; set; }
    public Guid TemplateId { get; set; }
    [ForeignKey(nameof(TemplateId))]
    public TaskTemplate? Template { get; set; }
    public DateOnly Day { get; set; }
    public TaskTimeSlot? TimeSlot { get; set; }
    public bool Completed { get; set; }
    public string? Notes { get; set; }
    public bool IsException { get; set; }
    public string? ExceptionReason { get; set; }
    public ICollection<TaskIntent> Intents { get; set; } = new List<TaskIntent>();
}
```

---

## 3. Rename `TaskAssignment` → `TaskIntent`

**Delete:** `src/Gatherstead.Data/Entities/TaskAssignment.cs`
**Create:** `src/Gatherstead.Data/Entities/TaskIntent.cs`

Hierarchy: `TaskIntent` belongs to `TaskPlan`. Consistent with `MealIntent` / `AccommodationIntent` pattern.

```csharp
[Index(nameof(TenantId), nameof(HouseholdMemberId))]
[Index(nameof(TenantId), nameof(TaskPlanId), nameof(HouseholdMemberId), IsUnique = true)]
public class TaskIntent : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    [ForeignKey(nameof(TenantId))]
    public Tenant? Tenant { get; set; }
    public Guid TaskPlanId { get; set; }
    [ForeignKey(nameof(TaskPlanId))]
    public TaskPlan? TaskPlan { get; set; }
    public Guid HouseholdMemberId { get; set; }
    [ForeignKey(nameof(HouseholdMemberId))]
    public HouseholdMember? HouseholdMember { get; set; }
    public bool Volunteered { get; set; }
}
```

---

## 4. Update `TaskTemplate` — `src/Gatherstead.Data/Entities/TaskTemplate.cs`

- `TaskTimeSlot TimeSlot` → `TaskTimeSlotFlags TimeSlots`
- Navigation `ICollection<TaskTask> Tasks` → `ICollection<TaskPlan> Plans`
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

`ICollection<TaskTemplate> TaskTemplates` stays as-is.

---

## 9. Update `GathersteadDbContext` — `src/Gatherstead.Data/GathersteadDbContext.cs`

```csharp
// Replace:
public DbSet<TaskTask> TaskTasks => Set<TaskTask>();
public DbSet<TaskAssignment> TaskAssignments => Set<TaskAssignment>();
// With:
public DbSet<TaskPlan> TaskPlans => Set<TaskPlan>();
public DbSet<TaskIntent> TaskIntents => Set<TaskIntent>();

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

GenerateTaskPlansAsync(addedDays)   — per TaskTemplate × ExpandSlots(template.TimeSlots)
GenerateMealPlansAsync(addedDays)    — per MealTemplate × ExpandMealTypes(template.MealTypes)
PruneTaskPlansAsync(removedDays)    — safe prune: !Completed && !IsException && no Intents
PruneMealPlansAsync(removedDays)     — safe prune: !IsException && no Intents
```

### Generation idempotency:
Generation queries that need to see soft-deleted records use the `_includeDeleted` toggle on `GathersteadDbContext` (consistent with the composable filter pattern in `ARCHITECTURE.md` — never use `IgnoreQueryFilters()` as it would strip tenant isolation):
- Soft-deleted + `IsException = true` → **skip** (suppression marker — user explicitly excluded this day/slot)
- Soft-deleted + `IsException = false` → **restore** (un-delete — day was pruned when dates shrank, now restored)
- Active record exists → **leave it alone**
- No record → **insert new TaskPlan/MealPlan**

### Suppression markers (pre-emptive exceptions):
A soft-deleted `TaskPlan`/`MealPlan` with `IsException = true` and `IsDeleted = true` acts as a tombstone. Generation skips that combination, handling "don't generate first breakfast" without a separate entity.

### Helper methods:
```csharp
private static IEnumerable<TaskTimeSlot> ExpandSlots(TaskTimeSlotFlags flags)
{
    if (flags.HasFlag(TaskTimeSlotFlags.Anytime)) return [TaskTimeSlot.Anytime];
    var result = new List<TaskTimeSlot>();
    if (flags.HasFlag(TaskTimeSlotFlags.Morning)) result.Add(TaskTimeSlot.Morning);
    if (flags.HasFlag(TaskTimeSlotFlags.Midday))  result.Add(TaskTimeSlot.Midday);
    if (flags.HasFlag(TaskTimeSlotFlags.Evening)) result.Add(TaskTimeSlot.Evening);
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
public record TaskPlanDiff
{
    // New slots to insert as TaskPlan records
    public IReadOnlyList<(DateOnly Day, TaskTimeSlot Slot)> ToAdd { get; init; } = [];
    // Soft-deleted (!IsException) TaskPlans to restore (un-delete)
    public IReadOnlyList<TaskPlan> ToRestore { get; init; } = [];
    // Active (!Completed, !IsException, no Intents) TaskPlans to soft-delete
    public IReadOnlyList<TaskPlan> ToPrune { get; init; } = [];
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
    public static TaskPlanDiff DiffTaskPlans(
        TaskTimeSlotFlags timeSlots,
        DateOnly start,
        DateOnly end,
        IEnumerable<TaskPlan> existingPlans);

    public static MealPlanDiff DiffMealPlans(
        MealTypeFlags mealTypes,
        DateOnly start,
        DateOnly end,
        IEnumerable<MealPlan> existingPlans);

    // Expand a flags value into individual enum values
    public static IEnumerable<TaskTimeSlot> ExpandSlots(TaskTimeSlotFlags flags);
    public static IEnumerable<MealType> ExpandMealTypes(MealTypeFlags flags);
    public static IEnumerable<DateOnly> GetDateRange(DateOnly start, DateOnly end);
}
```

### Diff logic (inside `DiffTaskPlans`):

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

When a `TaskTemplate.TimeSlots` or `MealTemplate.MealTypes` is updated (not just event date changes), the same utilities regenerate plans for the full event range:

```csharp
// In a future TaskTemplateService or maintenance job:
var diff = PlanGenerator.DiffTaskPlans(
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
// - 3-day event, Morning|Evening → 6 TaskPlans produced
// - Suppress day 2 Evening via tombstone → 5 TaskPlans, tombstone slot skipped
// - Extend event by 1 day → only the 2 new-day slots in ToAdd, existing untouched
// - Shorten event by 1 day → last-day plans with no data in ToPrune; completed plan stays
// - Change template from Morning to Morning|Midday → Midday slots for all days in ToAdd
```

---

## 12. Docs Updates

### `docs/ARCHITECTURE.md` — Gathering Planning Context section

Replace `MealPlan`, `MealIntent`, `TaskTemplate`, `TaskTask` bullets with:

```
- **MealTemplate**: Template scoped to an event specifying which meal types (Breakfast/Lunch/Dinner via MealTypeFlags) to generate across the event's date range.
- **MealPlan**: A specific meal on a specific day, owned by a MealTemplate. Supports exception marking to suppress auto-generated entries. Aggregates meal intents.
- **MealIntent**: Member-level response indicating attendance for a meal, dietary considerations, and bring-your-own-food choices.
- **TaskTemplate**: Template for recurring tasks across an event; specifies one or more time slots (Morning/Midday/Evening/Anytime via TaskTimeSlotFlags) and drives automatic TaskPlan generation.
- **TaskPlan**: Dated task instance for a specific day and time slot, owned by a TaskTemplate. Supports exception marking and completion tracking. (Renamed from TaskTask.)
- **TaskIntent**: Member's volunteer/assignment record for a TaskPlan. (Renamed from TaskAssignment; consistent with MealIntent/AccommodationIntent pattern.)
```

### `docs/IMPLEMENTATION_STATUS.md`

- In **Implemented Features**: update description of gathering planning entities to use `TaskPlan`, `TaskIntent`, `MealTemplate`; remove `TaskTask`/`TaskAssignment` references.
- In **Planned Enhancements**:
  - Update "Task sign-up flows" to reference `TaskIntent` and `TaskPlan`
  - Update "Gathering Planning API" to reference `MealTemplate`, exception tracking, and hierarchical meal/task ownership
  - Add: **Event plan auto-generation**: When an event's date range changes, `TaskPlan` and `MealPlan` records are automatically generated for added days (per template configuration) or safely pruned for removed days (only if no user data — intents, completion — and `IsException = false`).

---

## 12. Files Changed / Created

| Action | Path |
|--------|------|
| Modify | `src/Gatherstead.Data/Entities/Enums.cs` |
| Rename+modify | `src/Gatherstead.Data/Entities/TaskTask.cs` → `TaskPlan.cs` |
| Rename+modify | `src/Gatherstead.Data/Entities/TaskAssignment.cs` → `TaskIntent.cs` |
| Modify | `src/Gatherstead.Data/Entities/TaskTemplate.cs` |
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
   - 3-day event, `Morning|Evening` → 6 TaskPlan slots in `ToAdd`
   - Suppression marker present → that slot absent from `ToAdd`
   - Extend date range → only new-day slots in `ToAdd`, existing untouched
   - Shorten date range → no-data plans in `ToPrune`; completed/exception/intent-bearing plans omitted from `ToPrune`
   - Template slot change (`Morning` → `Morning|Midday`) → `Midday` slots for all existing days in `ToAdd`
3. Integration test: create Event with MealTemplate (Breakfast|Dinner) + TaskTemplate (Morning|Evening); confirm generation produces correct TaskPlan and MealPlan rows
4. Integration test: extend event EndDate → new plans generated for added days
5. Integration test: shorten event EndDate → only unworked, intent-free, non-exception plans pruned
6. Integration test: create suppression marker for day+slot → generation skips that combination
7. Confirm `docs/ARCHITECTURE.md` entity descriptions match updated class/property names
