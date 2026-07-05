// Shared, canonical ordering for the entity lists rendered across the app. Applied in the
// data composables so every render site (and demo mode) sorts identically. Names use
// locale-aware, case-insensitive comparison; unknown/null keys sort last so partial data
// never jumps to the top.
import type { AccommodationType, AgeBand } from '~/repositories/types'

export const ACCOMMODATION_TYPE_ORDER: AccommodationType[] = ['Bedroom', 'Bunk', 'RvPad', 'Tent', 'Offsite']
export const AGE_BAND_ORDER: AgeBand[] = ['Age0To2', 'Age3To5', 'Age6To12', 'Age13To17', 'Age18To64', 'Age65Plus']

const ranker = <T>(order: readonly T[]) => (v: T | null | undefined) => {
  const i = v == null ? -1 : order.indexOf(v)
  return i < 0 ? order.length : i
}
export const accommodationTypeRank = ranker(ACCOMMODATION_TYPE_ORDER)
export const ageBandRank = ranker(AGE_BAND_ORDER)

export const byName = (a: string | null | undefined, b: string | null | undefined) =>
  (a ?? '').localeCompare(b ?? '', undefined, { sensitivity: 'base' })

export const compareAccommodations = (
  a: { type: AccommodationType, name: string },
  b: { type: AccommodationType, name: string },
) => accommodationTypeRank(a.type) - accommodationTypeRank(b.type) || byName(a.name, b.name)

export const compareHouseholds = (a: { name: string }, b: { name: string }) => byName(a.name, b.name)

// Tenant-wide availability search spans every property, so results group by property first, then
// fall back to the standard accommodation ordering (type, then name) within each property.
export const compareAvailability = (
  a: { propertyName: string, type: AccommodationType, name: string },
  b: { propertyName: string, type: AccommodationType, name: string },
) => byName(a.propertyName, b.propertyName) || compareAccommodations(a, b)

export const compareMembers = (
  a: { ageBand: AgeBand | null, name: string },
  b: { ageBand: AgeBand | null, name: string },
) => ageBandRank(a.ageBand) - ageBandRank(b.ageBand) || byName(a.name, b.name)
