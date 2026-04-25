export function enumDays(startDate: string, endDate: string): string[] {
  const days: string[] = []
  const d = new Date(startDate)
  const end = new Date(endDate)
  while (d <= end) {
    days.push(d.toISOString().substring(0, 10))
    d.setDate(d.getDate() + 1)
  }
  return days
}
