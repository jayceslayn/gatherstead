import type { EventReportDay, EventReportTask, EventReportAccommodation, EventReportMeal } from '~/repositories/types'
import { compareOrderKeys, mealSlotRank, planAggregate, taskSlotRank } from '~/composables/useTemplateOrder'

// ── Coverage / occupancy derivation ─────────────────────────────────────────
// Kept in one place so the desktop strip, mobile pager, and print stack never
// disagree about what counts as covered or full.

export type CoverageStatus = 'Covered' | 'Partial' | 'Open' | 'Exception'

/** Maps a task plan's assignee count against its minimum to a GsStatusBadge status. */
export function taskCoverage(task: EventReportTask): CoverageStatus {
  if (task.isException) return 'Exception'
  if (task.completed) return 'Covered'
  const min = task.minimumAssignees
  if (min == null) return task.assigneeCount > 0 ? 'Covered' : 'Open'
  if (task.assigneeCount >= min) return 'Covered'
  if (task.assigneeCount > 0) return 'Partial'
  return 'Open'
}

/**
 * Badge colour for a task's minimum-assignee coverage, shared by the report and
 * the event sign-up so both highlight identically: green (met), amber (partial),
 * red (none yet). Tasks with no minimum stay neutral (white) — coverage is N/A.
 */
export function taskCoverageColor(
  assigneeCount: number,
  minimumAssignees: number | null | undefined,
): 'neutral' | 'success' | 'warning' | 'error' {
  if (minimumAssignees == null) return 'neutral'
  if (assigneeCount >= minimumAssignees) return 'success'
  if (assigneeCount > 0) return 'warning'
  return 'error'
}

export type OccupancyState = 'vacant' | 'partial' | 'full' | 'over' | 'unknown'

export interface Occupancy {
  capacity: number | null
  occupied: number
  state: OccupancyState
}

/** Occupancy-state → badge colour, shared by the report and the event sign-up. */
export const OCCUPANCY_COLOR: Record<OccupancyState, 'neutral' | 'success' | 'warning' | 'error'> = {
  vacant: 'neutral',
  partial: 'success',
  full: 'warning',
  over: 'error',
  unknown: 'neutral',
}

/** Derives occupancy state from raw counts. Capacity null → 'unknown' (count only, no colour). */
export function occupancyState(occupied: number, capacity: number | null): OccupancyState {
  if (capacity == null) return 'unknown'
  if (occupied === 0) return 'vacant'
  if (occupied > capacity) return 'over'
  if (occupied >= capacity) return 'full'
  return 'partial'
}

/** Sleeps capacity vs occupied party size. Capacity null → state is 'unknown' (count only, no colour). */
export function accommodationOccupancy(acc: EventReportAccommodation): Occupancy {
  const capacity = acc.capacity ?? null
  return { capacity, occupied: acc.occupied, state: occupancyState(acc.occupied, capacity) }
}

// ── Day → lane pivots ────────────────────────────────────────────────────
// The report API is day-keyed (`EventReportDay[]`); the swimlane layout needs
// one lane per recurring entity (meal slot / task plan / accommodation) with a
// per-day entry. These pivots group the flat day arrays into lanes once so the
// report grid doesn't re-scan all days per render.

export interface ReportLane<T> {
  key: string
  title: string
  subtitle?: string
  byDay: Record<string, T>
}

/** Per-day going/maybe totals, keyed by day — feeds the swimlane header. */
export function reportDayTotals(days: EventReportDay[]): Record<string, { going: number, maybe: number }> {
  const result: Record<string, { going: number, maybe: number }> = {}
  for (const d of days) result[d.day] = { going: d.going, maybe: d.maybe }
  return result
}

/**
 * Groups each day's meals into lanes by meal type + template (the recurring "slot"),
 * ordered by the shared template scheme (slot → earliest effective day → most effective
 * plans → title) so the report matches the sign-up and management views.
 */
export function buildMealLanes(days: EventReportDay[]): ReportLane<EventReportMeal>[] {
  const lanes = new Map<string, ReportLane<EventReportMeal>>()
  for (const d of days) {
    for (const meal of d.meals) {
      const key = `${meal.mealType}:${meal.templateId}`
      let lane = lanes.get(key)
      if (!lane) {
        lane = { key, title: meal.templateName, subtitle: meal.mealType, byDay: {} }
        lanes.set(key, lane)
      }
      lane.byDay[d.day] = meal
    }
  }
  return [...lanes.values()].sort((a, b) => {
    const aSlot = Object.values(a.byDay)[0]!.mealType
    const bSlot = Object.values(b.byDay)[0]!.mealType
    return compareOrderKeys(laneOrderKey(a, mealSlotRank(aSlot)), laneOrderKey(b, mealSlotRank(bSlot)))
  })
}

/**
 * Groups each day's tasks into lanes by template + time slot (the recurring task plan),
 * ordered by the shared template scheme (slot → earliest effective day → most effective
 * plans → title).
 */
export function buildTaskLanes(days: EventReportDay[]): ReportLane<EventReportTask>[] {
  const lanes = new Map<string, ReportLane<EventReportTask>>()
  for (const d of days) {
    for (const task of d.tasks) {
      const key = `${task.templateId}:${task.timeSlot ?? ''}`
      let lane = lanes.get(key)
      if (!lane) {
        lane = { key, title: task.templateName, subtitle: task.timeSlot ?? undefined, byDay: {} }
        lanes.set(key, lane)
      }
      lane.byDay[d.day] = task
    }
  }
  return [...lanes.values()].sort((a, b) => {
    const aSlot = Object.values(a.byDay)[0]!.timeSlot
    const bSlot = Object.values(b.byDay)[0]!.timeSlot
    return compareOrderKeys(laneOrderKey(a, taskSlotRank(aSlot)), laneOrderKey(b, taskSlotRank(bSlot)))
  })
}

/** Builds an OrderKey from a lane's per-day cells (day = key, exception flag off the cell). */
function laneOrderKey(
  lane: ReportLane<{ isException?: boolean }>,
  slotRank: number,
): { slotRank: number, firstEffectiveDay: string, effectiveCount: number, title: string } {
  const plans = Object.entries(lane.byDay).map(([day, cell]) => ({ day, isException: cell.isException }))
  return { slotRank, ...planAggregate(plans), title: lane.title }
}

/** Groups each day's accommodations into lanes by accommodation id. */
export function buildAccommodationLanes(days: EventReportDay[]): ReportLane<EventReportAccommodation>[] {
  const lanes = new Map<string, ReportLane<EventReportAccommodation>>()
  for (const d of days) {
    for (const acc of d.accommodations) {
      let lane = lanes.get(acc.accommodationId)
      if (!lane) {
        lane = { key: acc.accommodationId, title: acc.name, byDay: {} }
        lanes.set(acc.accommodationId, lane)
      }
      lane.byDay[d.day] = acc
    }
  }
  return [...lanes.values()]
}
