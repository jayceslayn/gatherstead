export function useFormatDate() {
  const { locale, t } = useI18n()

  function formatDate(date: string) {
    return new Intl.DateTimeFormat(locale.value, {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
    }).format(new Date(date + 'T00:00:00'))
  }

  function formatDay(date: string) {
    return new Intl.DateTimeFormat(locale.value, {
      weekday: 'long',
      month: 'long',
      day: 'numeric',
    }).format(new Date(date + 'T00:00:00'))
  }

  // Single-day events (start === end) show just one date instead of a "X – X" range.
  function formatDateRange(start: string, end: string) {
    return start === end
      ? formatDate(start)
      : t('event.dateRange', { start: formatDate(start), end: formatDate(end) })
  }

  return { formatDate, formatDay, formatDateRange }
}
