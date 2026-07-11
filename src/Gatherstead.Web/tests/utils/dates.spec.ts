import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { rangesOverlap, today, todayOffset } from '../../app/utils/dates'

describe('rangesOverlap', () => {
  it('returns true when ranges partially overlap', () => {
    expect(rangesOverlap('2026-07-01', '2026-07-05', '2026-07-04', '2026-07-08')).toBe(true)
  })

  it('treats a shared boundary day as an overlap (inclusive)', () => {
    // a ends the day b starts
    expect(rangesOverlap('2026-07-01', '2026-07-05', '2026-07-05', '2026-07-08')).toBe(true)
    // b ends the day a starts
    expect(rangesOverlap('2026-07-05', '2026-07-08', '2026-07-01', '2026-07-05')).toBe(true)
  })

  it('returns false when ranges are disjoint', () => {
    // a entirely before b
    expect(rangesOverlap('2026-07-01', '2026-07-04', '2026-07-05', '2026-07-08')).toBe(false)
    // a entirely after b
    expect(rangesOverlap('2026-07-10', '2026-07-12', '2026-07-01', '2026-07-05')).toBe(false)
  })

  it('returns true when one range fully contains the other', () => {
    expect(rangesOverlap('2026-07-01', '2026-07-31', '2026-07-10', '2026-07-12')).toBe(true)
    expect(rangesOverlap('2026-07-10', '2026-07-12', '2026-07-01', '2026-07-31')).toBe(true)
  })

  it('handles single-day ranges on a boundary', () => {
    // a single day inside b
    expect(rangesOverlap('2026-07-05', '2026-07-05', '2026-07-01', '2026-07-08')).toBe(true)
    // a single day just outside b
    expect(rangesOverlap('2026-07-09', '2026-07-09', '2026-07-01', '2026-07-08')).toBe(false)
  })
})

describe('today / todayOffset', () => {
  beforeEach(() => {
    vi.useFakeTimers()
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('formats as YYYY-MM-DD', () => {
    vi.setSystemTime(new Date(2026, 6, 10, 12, 0, 0)) // local noon, July 10
    expect(today()).toBe('2026-07-10')
  })

  it('uses the local day, not the UTC day, near midnight', () => {
    // 11pm local on July 10: in any timezone west of UTC the UTC date is already July 11,
    // but the user-perceived day is still the 10th.
    vi.setSystemTime(new Date(2026, 6, 10, 23, 0, 0))
    expect(today()).toBe('2026-07-10')
  })

  it('offsets by days, crossing month boundaries', () => {
    vi.setSystemTime(new Date(2026, 6, 31, 12, 0, 0)) // July 31
    expect(todayOffset(1)).toBe('2026-08-01')
    expect(todayOffset(-1)).toBe('2026-07-30')
    expect(todayOffset(0)).toBe(today())
  })
})
