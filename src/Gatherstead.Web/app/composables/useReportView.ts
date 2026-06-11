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

export type OccupancyState = 'vacant' | 'partial' | 'full' | 'over' | 'unknown'

export interface Occupancy {
  capacity: number | null
  occupied: number
  state: OccupancyState
}

/** Total capacity vs occupied party size. Capacity null → state is 'unknown' (count only, no colour). */
export function accommodationOccupancy(acc: EventReportAccommodation): Occupancy {
  const capacity = acc.capacityAdults != null || acc.capacityChildren != null
    ? (acc.capacityAdults ?? 0) + (acc.capacityChildren ?? 0)
    : null

  let state: OccupancyState
  if (capacity == null) state = 'unknown'
  else if (acc.occupied === 0) state = 'vacant'
  else if (acc.occupied > capacity) state = 'over'
  else if (acc.occupied >= capacity) state = 'full'
  else state = 'partial'

  return { capacity, occupied: acc.occupied, state }
}
