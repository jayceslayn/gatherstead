import type { IHouseholdMemberRepository } from '../interfaces'
import type { HouseholdMember, AgeBand, AttributeWriteEntry, AttributeEntry } from '../types'
import { deriveAgeBand } from '../types'
import { getDemoStore, persistDemoStore, demoId, DEMO_LIMITS, DemoLimitError } from './DemoStore'
import { DEMO_AGE_BANDS } from './DemoAgeBandRepository'

function toAttributeEntries(writes: AttributeWriteEntry[] | null | undefined): AttributeEntry[] {
  if (!writes) return []
  return writes.map(w => ({ id: demoId(), key: w.key, value: w.value, tenantMinRole: w.tenantMinRole, householdMinRole: w.householdMinRole ?? null }))
}

// Adult from the 18–64 band upward, mirroring the backend AgeBands.IsAdult.
const ADULT_BANDS = new Set<AgeBand>(['Age18To64', 'Age65Plus'])
function isAdultFromBand(band: AgeBand | null): boolean | null {
  return band == null ? null : ADULT_BANDS.has(band)
}

// Mirrors the backend: when a birth date is present the age band is derived and
// authoritative; the stored manual band is only used as a fallback when no birth date.
// isAdult is always derived from the effective band (null when neither is set).
function withDerivedAgeBand(m: HouseholdMember): HouseholdMember {
  const ageBand = m.birthDate ? deriveAgeBand(m.birthDate, DEMO_AGE_BANDS) : m.ageBand
  return { ...m, ageBand, isAdult: isAdultFromBand(ageBand) }
}

// Store the manual band only when there's no birth date (the backend nulls it otherwise).
function bandForStorage(ageBand: string | null, birthDate: string | null): AgeBand | null {
  return birthDate ? null : (ageBand as AgeBand | null)
}

export class DemoHouseholdMemberRepository implements IHouseholdMemberRepository {
  async listMembers(_tenantId: string, householdId: string): Promise<HouseholdMember[]> {
    return getDemoStore().members.value
      .filter(m => m.householdId === householdId)
      .map(withDerivedAgeBand)
  }

  async getMember(_tenantId: string, householdId: string, memberId: string): Promise<HouseholdMember | null> {
    const m = getDemoStore().members.value.find(
      x => x.householdId === householdId && x.id === memberId,
    )
    return m ? withDerivedAgeBand(m) : null
  }

  async createMember(
    tenantId: string,
    householdId: string,
    name: string,
    ageBand: string | null,
    birthDate: string | null,
    dietaryNotes: string | null,
    notes: string | null,
    dietaryTags: string[],
    attributes?: AttributeWriteEntry[] | null,
  ): Promise<HouseholdMember> {
    const store = getDemoStore()
    if (store.members.value.filter(m => m.householdId === householdId).length >= DEMO_LIMITS.membersPerHousehold) {
      throw new DemoLimitError('membersPerHousehold')
    }
    const m: HouseholdMember = {
      id: demoId(),
      tenantId,
      householdId,
      name,
      // isAdult is derived from the band on read; store null.
      isAdult: null,
      ageBand: bandForStorage(ageBand, birthDate),
      birthDate,
      dietaryNotes,
      notes,
      dietaryTags,
      attributes: toAttributeEntries(attributes),
    }
    store.members.value.push(m)
    persistDemoStore()
    return withDerivedAgeBand(m)
  }

  async updateMember(
    _tenantId: string,
    _householdId: string,
    memberId: string,
    name: string,
    ageBand: string | null,
    birthDate: string | null,
    dietaryNotes: string | null,
    notes: string | null,
    dietaryTags: string[],
    attributes?: AttributeWriteEntry[] | null,
  ): Promise<void> {
    const store = getDemoStore()
    const m = store.members.value.find(x => x.id === memberId)
    if (!m) return
    m.name = name
    m.ageBand = bandForStorage(ageBand, birthDate)
    m.birthDate = birthDate
    m.dietaryNotes = dietaryNotes
    m.notes = notes
    m.dietaryTags = dietaryTags
    if (attributes !== undefined) m.attributes = toAttributeEntries(attributes)
    persistDemoStore()
  }

  async deleteMember(_tenantId: string, _householdId: string, memberId: string): Promise<void> {
    const store = getDemoStore()
    store.members.value = store.members.value.filter(m => m.id !== memberId)
    persistDemoStore()
  }
}
