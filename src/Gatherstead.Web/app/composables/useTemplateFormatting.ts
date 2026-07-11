import type { MealType, TaskTimeSlot } from '~/repositories/types'
import { mealTypesFromFlags, taskSlotsFromFlags } from '~/repositories/types'

/**
 * Shared display helpers for meal/task template cards. Used by the event
 * create (drafts) and edit (persisted) pages so the formatting stays in sync.
 */
export function useTemplateFormatting() {
  const { t, locale } = useI18n()

  function formatRange(start: string | null, end: string | null): string | null {
    if (!start || !end) return null
    const fmt = (d: string) =>
      new Intl.DateTimeFormat(locale.value, { weekday: 'short', month: 'short', day: 'numeric' }).format(
        new Date(d + 'T00:00:00'),
      )
    return start === end ? fmt(start) : t('event.meal.dateRange', { start: fmt(start), end: fmt(end) })
  }

  /** Label for a single meal time slot (`event.meal.breakfast` etc.). */
  function mealTypeLabel(mealType: MealType): string {
    return t(`event.meal.${mealType.toLowerCase()}`)
  }

  /** Label for a single task time slot (`event.task.morning` etc.). */
  function taskSlotLabel(slot: TaskTimeSlot): string {
    return t(`event.task.${slot.toLowerCase()}`)
  }

  function mealTypeLabels(flags: number): string {
    return mealTypesFromFlags(flags).map(mealTypeLabel).join(', ')
  }

  function taskSlotLabels(flags: number): string {
    return taskSlotsFromFlags(flags).map(taskSlotLabel).join(', ')
  }

  return { formatRange, mealTypeLabel, taskSlotLabel, mealTypeLabels, taskSlotLabels }
}
