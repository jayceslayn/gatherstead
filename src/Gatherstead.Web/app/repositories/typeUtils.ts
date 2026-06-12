import type { components } from './generated/api'
import { MEAL_TYPE_FLAGS, TASK_SLOT_FLAGS } from './generated/flags.gen'

type S = components['schemas']

// openapi-typescript inlines enum values rather than naming them — extract from a DTO field.
export type MealType = NonNullable<S['MealPlanDto']['mealType']>
export type TaskTimeSlot = NonNullable<S['TaskPlanDto']['timeSlot']>

type AgeBand = NonNullable<S['HouseholdMemberDto']['ageBand']>
type AgeBandOption = S['AgeBandOptionDto']

export { MEAL_TYPE_FLAGS, TASK_SLOT_FLAGS }

export const ALL_MEAL_TYPES: MealType[] = ['Breakfast', 'Lunch', 'Dinner']
export const ALL_TASK_SLOTS: TaskTimeSlot[] = ['Morning', 'Midday', 'Evening', 'Anytime']

export function mealTypesFromFlags(flags: number): MealType[] {
  return ALL_MEAL_TYPES.filter(t => (flags & MEAL_TYPE_FLAGS[t]) !== 0)
}

export function taskSlotsFromFlags(flags: number): TaskTimeSlot[] {
  return ALL_TASK_SLOTS.filter(s => (flags & TASK_SLOT_FLAGS[s]) !== 0)
}

// Maps meal-type flags to the equivalent task time-slot flags (Breakfast→Morning, Lunch→Midday,
// Dinner→Evening), falling back to Anytime when nothing maps. Mirrors the backend
// MealTemplateService.MapMealTypesToTimeSlots so the demo "matching task" behaves identically —
// and doesn't depend on the two flag sets happening to share bit values.
export function mealTypeFlagsToTaskSlotFlags(mealTypes: number): number {
  let slots = 0
  if (mealTypes & MEAL_TYPE_FLAGS.Breakfast) slots |= TASK_SLOT_FLAGS.Morning
  if (mealTypes & MEAL_TYPE_FLAGS.Lunch) slots |= TASK_SLOT_FLAGS.Midday
  if (mealTypes & MEAL_TYPE_FLAGS.Dinner) slots |= TASK_SLOT_FLAGS.Evening
  return slots === 0 ? TASK_SLOT_FLAGS.Anytime : slots
}

// Buckets a birth date into an age band using the band ranges the API supplies
// (minAge/maxAge). The band *definitions* stay authoritative on the backend — this
// only applies them client-side so the member form can preview the band live and the
// demo repo (which has no backend) can derive it the same way. Mirrors
// HouseholdMember.AgeBands.DeriveFromBirthDate.
export function deriveAgeBand(
  birthDate: string | null | undefined,
  options: readonly AgeBandOption[],
): AgeBand | null {
  if (!birthDate) return null
  const today = new Date()
  const birth = new Date(birthDate + 'T00:00:00')
  let age = today.getFullYear() - birth.getFullYear()
  const m = today.getMonth() - birth.getMonth()
  if (m < 0 || (m === 0 && today.getDate() < birth.getDate())) age--
  const match = options.find(o => age >= o.minAge && (o.maxAge == null || age <= o.maxAge))
  return match?.value ?? null
}
