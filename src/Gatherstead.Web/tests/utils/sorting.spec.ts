import { describe, expect, it } from 'vitest'
import { compareHouseholds, compareMembers } from '../../app/utils/sorting'
import type { AgeBand } from '../../app/repositories/types'

type Member = { ageBand: AgeBand | null, birthDate?: string | null, name: string }

const orderNames = (members: Member[]) => [...members].sort(compareMembers).map(m => m.name)

describe('compareMembers', () => {
  it('orders oldest age band first, unknown band last', () => {
    const members: Member[] = [
      { ageBand: 'Age0To2', name: 'Ivy' },
      { ageBand: 'Age65Plus', name: 'Sam' },
      { ageBand: null, name: 'Unknown' },
      { ageBand: 'Age18To64', name: 'Bea' },
    ]
    expect(orderNames(members)).toEqual(['Sam', 'Bea', 'Ivy', 'Unknown'])
  })

  it('breaks ties within a band by most recent birth date, ahead of name', () => {
    const members: Member[] = [
      { ageBand: 'Age18To64', birthDate: '1970-05-01', name: 'Amy' },
      { ageBand: 'Age18To64', birthDate: '1990-05-01', name: 'Zoe' },
    ]
    // Zoe's birth date is more recent, so she leads despite sorting after Amy by name.
    expect(orderNames(members)).toEqual(['Zoe', 'Amy'])
  })

  it('sorts a missing birth date last within a band', () => {
    const members: Member[] = [
      { ageBand: 'Age18To64', birthDate: null, name: 'Amy' },
      { ageBand: 'Age18To64', birthDate: '1980-01-01', name: 'Zoe' },
    ]
    expect(orderNames(members)).toEqual(['Zoe', 'Amy'])
  })

  it('falls back to a case-insensitive name comparison', () => {
    const members: Member[] = [
      { ageBand: 'Age6To12', name: 'bob' },
      { ageBand: 'Age6To12', name: 'Amy' },
    ]
    expect(orderNames(members)).toEqual(['Amy', 'bob'])
  })
})

describe('compareHouseholds', () => {
  it('orders by name ascending, case-insensitive', () => {
    const households = [{ name: 'Zeta' }, { name: 'alpha' }, { name: 'Beta' }]
    expect([...households].sort(compareHouseholds).map(h => h.name)).toEqual(['alpha', 'Beta', 'Zeta'])
  })
})
