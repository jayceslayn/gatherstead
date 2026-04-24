import type { IHouseholdMemberRepository } from '../interfaces'
import type { HouseholdMember, DietaryProfile } from '../types'
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

  async createMember(tenantId: string, householdId: string, name: string): Promise<HouseholdMember> {
    const store = getDemoStore()
    if (store.members.value.filter(m => m.householdId === householdId).length >= DEMO_LIMITS.membersPerHousehold) {
      throw new DemoLimitError('membersPerHousehold')
    }
    const m: HouseholdMember = {
      id: demoId(),
      tenantId,
      householdId,
      name,
      isAdult: true,
      ageBand: null,
      birthDate: null,
      dietaryNotes: null,
      dietaryTags: [],
      householdRole: 'Member',
    }
    store.members.value.push(m)
    persistDemoStore()
    return m
  }
}
