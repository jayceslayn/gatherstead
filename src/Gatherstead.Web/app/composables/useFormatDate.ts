export function useFormatDate() {
  const { locale } = useI18n()

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

  return { formatDate, formatDay }
}
