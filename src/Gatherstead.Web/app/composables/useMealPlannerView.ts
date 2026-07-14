import type { AgeBand, EventReportAttendee, EventReportMeal } from '~/repositories/types'
import { AGE_BAND_ORDER } from '~/utils/sorting'

// ── Meal planner pivots ─────────────────────────────────────────────────────
// The report API delivers a flat per-meal attendee list; the meal planner needs
// attendees bucketed by effective age band, with dietary-combination counts within
// each band, so cooks can portion by age group. Pure derivations (like useReportView)
// so the page computes each pivot once per meal.

export interface MealDietaryGroup {
  /** Full sorted tag combination (e.g. "Gluten Free, Vegan"); '' when the group has no dietary tags — render via i18n, not this label. */
  label: string
  going: number
  maybe: number
}

export interface MealAgeBandGroup {
  /** Effective age band; null = unknown, ordered last. */
  band: AgeBand | null
  going: number
  maybe: number
  bringOwnFood: number
  dietary: MealDietaryGroup[]
  /** Band members in delivered order (household, then oldest first — server-sorted). */
  attendees: EventReportAttendee[]
}

// Oldest band first (matches the report's oldest-first member ordering); unknown last.
const bandRank = (band: AgeBand | null): number => {
  const i = band == null ? -1 : AGE_BAND_ORDER.indexOf(band)
  return i < 0 ? AGE_BAND_ORDER.length : AGE_BAND_ORDER.length - 1 - i
}

// Full sorted tag combination — the same grouping rule as the backend dietary tally, so a
// row answers "how many plates must satisfy this exact combination simultaneously".
const dietaryCombo = (attendee: EventReportAttendee): string =>
  attendee.dietary
    .map(d => d.trim())
    .filter(Boolean)
    .sort((a, b) => a.localeCompare(b, undefined, { sensitivity: 'base' }))
    .join(', ')

/**
 * Buckets a meal's attendees by effective age band (oldest first, unknown last), with
 * Going/Maybe counted separately (NotGoing is already excluded server-side) and dietary
 * combinations tallied within each band.
 */
export function buildAgeBandGroups(meal: EventReportMeal): MealAgeBandGroup[] {
  const groups = new Map<AgeBand | null, MealAgeBandGroup & { combos: Map<string, MealDietaryGroup> }>()
  for (const attendee of meal.attendees) {
    if (attendee.status === 'NotGoing') continue
    const band = attendee.ageBand ?? null
    let group = groups.get(band)
    if (!group) {
      group = { band, going: 0, maybe: 0, bringOwnFood: 0, dietary: [], attendees: [], combos: new Map() }
      groups.set(band, group)
    }

    const maybe = attendee.status === 'Maybe'
    if (maybe) group.maybe++
    else group.going++
    if (attendee.bringOwnFood) group.bringOwnFood++
    group.attendees.push(attendee)

    // Case-insensitive combo key (mirrors the backend tally's OrdinalIgnoreCase grouping);
    // the first-seen casing becomes the display label.
    const combo = dietaryCombo(attendee)
    const comboKey = combo.toLowerCase()
    let dietaryGroup = group.combos.get(comboKey)
    if (!dietaryGroup) {
      dietaryGroup = { label: combo, going: 0, maybe: 0 }
      group.combos.set(comboKey, dietaryGroup)
    }
    if (maybe) dietaryGroup.maybe++
    else dietaryGroup.going++
  }

  return [...groups.values()]
    .map(({ combos, ...group }) => ({
      ...group,
      // Largest combination first, then label — matches the report tally ordering.
      dietary: [...combos.values()].sort((a, b) =>
        (b.going + b.maybe) - (a.going + a.maybe)
        || a.label.localeCompare(b.label, undefined, { sensitivity: 'base' })),
    }))
    .sort((a, b) => bandRank(a.band) - bandRank(b.band))
}
