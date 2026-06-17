import type { MealType, TaskTimeSlot } from '~/repositories/types'
import { mealTypesFromFlags, taskSlotsFromFlags } from '~/repositories/typeUtils'

// ── Shared template/lane ordering ───────────────────────────────────────────
// One ordering scheme for meal & task templates everywhere (sign-up grids, the
// event report, the print stack, and the management lists) so the same template
// never appears in a different position depending on the page. Mirrors the C#
// ordering in EventReportService for the print/day arrays.
//
// A lane = (time slot, template). Lanes order by:
//   1. time slot
//   2. earliest "effective" (non-exception) plan day
//   3. most effective plans (descending)
//   4. template title (case-insensitive)

export const MEAL_SLOT_ORDER: MealType[] = ['Breakfast', 'Lunch', 'Dinner']
export const TASK_SLOT_ORDER: TaskTimeSlot[] = ['Morning', 'Midday', 'Evening', 'Anytime']

export function mealSlotRank(slot: MealType): number {
  const i = MEAL_SLOT_ORDER.indexOf(slot)
  return i === -1 ? MEAL_SLOT_ORDER.length : i
}

// 'Anytime' (and unslotted) tasks sort last — null ranks with 'Anytime'.
export function taskSlotRank(slot: TaskTimeSlot | null | undefined): number {
  if (!slot) return TASK_SLOT_ORDER.indexOf('Anytime')
  const i = TASK_SLOT_ORDER.indexOf(slot)
  return i === -1 ? TASK_SLOT_ORDER.length : i
}

// The lowest slot rank present in a template's bitmask — the template's "primary"
// slot, used by the management lists where a whole template is one orderable card.
export function mealTemplatePrimarySlotRank(mealTypes: number): number {
  const slots = mealTypesFromFlags(mealTypes)
  return slots.length ? Math.min(...slots.map(mealSlotRank)) : MEAL_SLOT_ORDER.length
}

export function taskTemplatePrimarySlotRank(timeSlots: number): number {
  const slots = taskSlotsFromFlags(timeSlots)
  return slots.length ? Math.min(...slots.map(taskSlotRank)) : TASK_SLOT_ORDER.length
}

export interface OrderKey {
  slotRank: number
  firstEffectiveDay: string
  effectiveCount: number
  title: string
}

// Sorts after any ISO date ('~' > digits), so planless templates sink to the bottom.
const NO_PLAN_DAY = '~'

export function compareOrderKeys(a: OrderKey, b: OrderKey): number {
  if (a.slotRank !== b.slotRank) return a.slotRank - b.slotRank
  if (a.firstEffectiveDay !== b.firstEffectiveDay) return a.firstEffectiveDay < b.firstEffectiveDay ? -1 : 1
  if (a.effectiveCount !== b.effectiveCount) return b.effectiveCount - a.effectiveCount
  return a.title.localeCompare(b.title, undefined, { sensitivity: 'base' })
}

interface PlanLike {
  day: string
  isException?: boolean | null
}

// Earliest effective (non-exception) plan day and the count of effective plans for a
// set of plans. Falls back to all plans when the set is exception-only so it still
// sorts deterministically; empty set yields the NO_PLAN_DAY sentinel.
export function planAggregate(plans: PlanLike[]): { firstEffectiveDay: string, effectiveCount: number } {
  const effective = plans.filter(p => !p.isException)
  const source = effective.length ? effective : plans
  const firstEffectiveDay = source.reduce((min, p) => (p.day < min ? p.day : min), NO_PLAN_DAY)
  return { firstEffectiveDay, effectiveCount: effective.length }
}
