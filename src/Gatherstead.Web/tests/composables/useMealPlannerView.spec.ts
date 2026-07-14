import { describe, expect, it } from 'vitest'
import { buildAgeBandGroups } from '../../app/composables/useMealPlannerView'
import type { EventReportAttendee, EventReportMeal } from '../../app/repositories/types'

const attendee = (overrides: Partial<EventReportAttendee> & { name: string }): EventReportAttendee => ({
  memberId: overrides.name,
  status: 'Going',
  bringOwnFood: false,
  ageBand: null,
  dietary: [],
  dietaryNotes: null,
  ...overrides,
})

const meal = (attendees: EventReportAttendee[]): EventReportMeal => ({
  mealPlanId: 'plan-1',
  templateId: 'template-1',
  templateName: 'Camp Dinner',
  mealType: 'Dinner',
  isException: false,
  notes: null,
  going: attendees.filter(a => a.status === 'Going').length,
  maybe: attendees.filter(a => a.status === 'Maybe').length,
  notGoing: 0,
  bringOwnFood: attendees.filter(a => a.bringOwnFood).length,
  dietary: [],
  attendees,
})

describe('buildAgeBandGroups', () => {
  it('buckets by age band oldest first, unknown band last', () => {
    const groups = buildAgeBandGroups(meal([
      attendee({ name: 'Ivy', ageBand: 'Age0To2' }),
      attendee({ name: 'Unknown', ageBand: null }),
      attendee({ name: 'Sam', ageBand: 'Age65Plus' }),
      attendee({ name: 'Bea', ageBand: 'Age18To64' }),
    ]))
    expect(groups.map(g => g.band)).toEqual(['Age65Plus', 'Age18To64', 'Age0To2', null])
  })

  it('counts going and maybe separately, and bring-own-food per band', () => {
    const groups = buildAgeBandGroups(meal([
      attendee({ name: 'Sam', ageBand: 'Age18To64' }),
      attendee({ name: 'Bea', ageBand: 'Age18To64', status: 'Maybe' }),
      attendee({ name: 'Pat', ageBand: 'Age18To64', bringOwnFood: true }),
    ]))
    const adults = groups.find(g => g.band === 'Age18To64')!
    expect(adults.going).toBe(2)
    expect(adults.maybe).toBe(1)
    expect(adults.bringOwnFood).toBe(1)
  })

  it('groups dietary needs by the full sorted tag combination within a band', () => {
    const groups = buildAgeBandGroups(meal([
      attendee({ name: 'Sam', ageBand: 'Age18To64', dietary: ['Vegan', 'Gluten Free'] }),
      attendee({ name: 'Bea', ageBand: 'Age18To64', dietary: ['gluten free', 'vegan'], status: 'Maybe' }),
      attendee({ name: 'Pat', ageBand: 'Age18To64', dietary: ['Vegan'] }),
      attendee({ name: 'Kid', ageBand: 'Age6To12', dietary: ['Vegan'] }),
    ]))
    const adults = groups.find(g => g.band === 'Age18To64')!
    // "Vegan, Gluten Free" and "gluten free, vegan" are the same combination (case-insensitive,
    // order-insensitive); Sam counts as going, Bea as maybe.
    const combo = adults.dietary.find(d => d.label.toLowerCase() === 'gluten free, vegan')!
    expect(combo.going).toBe(1)
    expect(combo.maybe).toBe(1)
    // Pat's single-tag combination is its own row; the child's identical tag stays in its own band.
    expect(adults.dietary.find(d => d.label === 'Vegan')).toMatchObject({ going: 1, maybe: 0 })
    expect(groups.find(g => g.band === 'Age6To12')!.dietary).toEqual([{ label: 'Vegan', going: 1, maybe: 0 }])
  })

  it('labels the no-restrictions combination with an empty string for i18n rendering', () => {
    const groups = buildAgeBandGroups(meal([
      attendee({ name: 'Sam', ageBand: 'Age18To64', dietary: [] }),
      attendee({ name: 'Bea', ageBand: 'Age18To64', dietary: [' '] }),
    ]))
    const adults = groups.find(g => g.band === 'Age18To64')!
    expect(adults.dietary).toEqual([{ label: '', going: 2, maybe: 0 }])
  })

  it('orders dietary rows largest combination first, then label', () => {
    const groups = buildAgeBandGroups(meal([
      attendee({ name: 'A', ageBand: 'Age18To64', dietary: ['Kosher'] }),
      attendee({ name: 'B', ageBand: 'Age18To64', dietary: ['Vegan'] }),
      attendee({ name: 'C', ageBand: 'Age18To64', dietary: ['Vegan'] }),
    ]))
    const adults = groups.find(g => g.band === 'Age18To64')!
    expect(adults.dietary.map(d => d.label)).toEqual(['Vegan', 'Kosher'])
  })

  it('preserves the delivered attendee order within a band', () => {
    // The server delivers attendees household-grouped, oldest first; the pivot must not re-sort.
    const groups = buildAgeBandGroups(meal([
      attendee({ name: 'Zoe', ageBand: 'Age18To64' }),
      attendee({ name: 'Amy', ageBand: 'Age18To64' }),
    ]))
    expect(groups[0]!.attendees.map(a => a.name)).toEqual(['Zoe', 'Amy'])
  })
})
