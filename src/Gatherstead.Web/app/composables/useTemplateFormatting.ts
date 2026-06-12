import { mealTypesFromFlags, taskSlotsFromFlags } from '~/repositories/types'

/**
 * Shared display helpers for meal/task template cards. Used by the event
 * create (drafts) and edit (persisted) pages so the formatting stays in sync.
 */
export function useTemplateFormatting() {
  const { t } = useI18n()

  function formatRange(start: string | null, end: string | null): string | null {
    if (!start || !end) return null
    const fmt = (d: string) =>
      new Intl.DateTimeFormat(undefined, { weekday: 'short', month: 'short', day: 'numeric' }).format(
        new Date(d + 'T00:00:00'),
      )
    return start === end ? fmt(start) : t('event.meal.dateRange', { start: fmt(start), end: fmt(end) })
  }

  function mealTypeLabels(flags: number): string {
    return mealTypesFromFlags(flags).map(mt => t(`event.meal.${mt.toLowerCase()}`)).join(', ')
  }

  function taskSlotLabels(flags: number): string {
    return taskSlotsFromFlags(flags).map(s => t(`event.task.${s.toLowerCase()}`)).join(', ')
  }

  return { formatRange, mealTypeLabels, taskSlotLabels }
}
