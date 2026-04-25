import type { IHouseholdMemberRepository } from '../interfaces'
import type { HouseholdMember, HouseholdRole, DietaryProfile } from '../types'
import { getDemoStore, persistDemoStore, demoId, DEMO_LIMITS, DemoLimitError } from './DemoStore'

export class DemoHouseholdMemberRepository implements IHouseholdMemberRepository {
  async listMembers(_tenantId: string, householdId: string): Promise<HouseholdMember[]> {
    return getDemoStore().members.value.filter(m => m.householdId === householdId)
  }

  async getMember(_tenantId: string, householdId: string, memberId: string): Promise<HouseholdMember | null> {
    return getDemoStore().members.value.find(
      m => m.householdId === householdId && m.id === memberId,
    ) ?? null
  }

  async getDietaryProfile(_tenantId: string, _householdId: string, _memberId: string): Promise<DietaryProfile | null> {
    return null
  }

  async createMember(
    tenantId: string,
    householdId: string,
    name: string,
    isAdult: boolean,
    ageBand: string | null,
    birthDate: string | null,
    householdRole: HouseholdRole,
    dietaryNotes: string | null,
    dietaryTags: string[],
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
      isAdult,
      ageBand,
      birthDate,
      dietaryNotes,
      dietaryTags,
      householdRole,
    }
    store.members.value.push(m)
    persistDemoStore()
    return m
  }

  async updateMember(
    _tenantId: string,
    _householdId: string,
    memberId: string,
    name: string,
    isAdult: boolean,
    ageBand: string | null,
    birthDate: string | null,
    householdRole: HouseholdRole,
    dietaryNotes: string | null,
    dietaryTags: string[],
  ): Promise<void> {
    const store = getDemoStore()
    const m = store.members.value.find(x => x.id === memberId)
    if (!m) return
    m.name = name
    m.isAdult = isAdult
    m.ageBand = ageBand
    m.birthDate = birthDate
    m.householdRole = householdRole
    m.dietaryNotes = dietaryNotes
    m.dietaryTags = dietaryTags
    persistDemoStore()
  }

  async deleteMember(_tenantId: string, _householdId: string, memberId: string): Promise<void> {
    const store = getDemoStore()
    store.members.value = store.members.value.filter(m => m.id !== memberId)
    persistDemoStore()
  }
}
