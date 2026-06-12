import type { IAgeBandRepository } from '../interfaces'
import type { AgeBandOption } from '../types'

export const DEMO_AGE_BANDS: AgeBandOption[] = [
  { value: 'Age0To2',   displayName: '0–2',   minAge: 0,  maxAge: 2,    sortOrder: 0 },
  { value: 'Age3To5',   displayName: '3–5',   minAge: 3,  maxAge: 5,    sortOrder: 1 },
  { value: 'Age6To12',  displayName: '6–12',  minAge: 6,  maxAge: 12,   sortOrder: 2 },
  { value: 'Age13To17', displayName: '13–17', minAge: 13, maxAge: 17,   sortOrder: 3 },
  { value: 'Age18To64', displayName: '18–64', minAge: 18, maxAge: 64,   sortOrder: 4 },
  { value: 'Age65Plus', displayName: '65+',        minAge: 65, maxAge: null, sortOrder: 5 },
]

export class DemoAgeBandRepository implements IAgeBandRepository {
  async listAgeBands(): Promise<AgeBandOption[]> {
    return DEMO_AGE_BANDS
  }
}
