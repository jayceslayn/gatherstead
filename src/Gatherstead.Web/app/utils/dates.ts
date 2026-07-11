// Date-range helpers. Dates are inclusive ISO `YYYY-MM-DD` strings, which compare lexicographically,
// so no Date parsing is needed for range math.

/**
 * Whether two inclusive date ranges overlap. Two spans overlap when each starts on or before the
 * other ends — a shared boundary day counts as an overlap.
 */
export function rangesOverlap(aStart: string, aEnd: string, bStart: string, bEnd: string): boolean {
  return aStart <= bEnd && bStart <= aEnd
}

/**
 * Today's date as `YYYY-MM-DD` in the user's local timezone (en-CA formats as ISO).
 * Local, not UTC: `toISOString()` flips to the wrong day for hours around local midnight
 * (e.g. a UTC-5 user at 8pm is already "tomorrow" in UTC).
 */
export function today(): string {
  return todayOffset(0)
}

/** `today()` shifted by `days` (negative for past), same local-timezone semantics. */
export function todayOffset(days: number): string {
  const d = new Date()
  d.setDate(d.getDate() + days)
  return d.toLocaleDateString('en-CA')
}
