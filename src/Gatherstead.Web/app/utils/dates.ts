// Date-range helpers. Dates are inclusive ISO `YYYY-MM-DD` strings, which compare lexicographically,
// so no Date parsing is needed for range math.

/**
 * Whether two inclusive date ranges overlap. Two spans overlap when each starts on or before the
 * other ends — a shared boundary day counts as an overlap.
 */
export function rangesOverlap(aStart: string, aEnd: string, bStart: string, bEnd: string): boolean {
  return aStart <= bEnd && bStart <= aEnd
}
