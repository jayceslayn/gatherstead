import type { components } from './generated/api'
import { MEAL_TYPE_FLAGS, TASK_SLOT_FLAGS } from './generated/flags.gen'

type S = components['schemas']

export type MealType = S['MealType']
export type TaskTimeSlot = S['TaskTimeSlot']

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
