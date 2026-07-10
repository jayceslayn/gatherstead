import { describe, expect, it } from 'vitest'
import { rangesOverlap } from '../../app/utils/dates'

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
