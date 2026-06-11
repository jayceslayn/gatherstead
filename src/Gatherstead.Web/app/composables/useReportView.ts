import type { EventReportTask, EventReportAccommodation } from '~/repositories/types'

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

/** Total capacity vs occupied party size. Capacity null → state is 'unknown' (count only, no colour). */
export function accommodationOccupancy(acc: EventReportAccommodation): Occupancy {
  const capacity = acc.capacityAdults != null || acc.capacityChildren != null
    ? (acc.capacityAdults ?? 0) + (acc.capacityChildren ?? 0)
    : null

  return { capacity, occupied: acc.occupied, state: occupancyState(acc.occupied, capacity) }
}
