import type { IHouseholdMemberRepository } from '../interfaces'
import type { HouseholdMember, DietaryProfile } from '../types'

interface MembersApiResponse {
  entity: HouseholdMember[]
  successful: boolean
}

interface MemberApiResponse {
  entity: HouseholdMember
  successful: boolean
}

interface DietaryProfileApiResponse {
  entity: DietaryProfile
  successful: boolean
}

export class LiveHouseholdMemberRepository implements IHouseholdMemberRepository {
  async listMembers(tenantId: string, householdId: string): Promise<HouseholdMember[]> {
    const r = await $fetch<MembersApiResponse>(
      `/api/proxy/tenants/${tenantId}/households/${householdId}/members`,
    )
    return r.entity ?? []
  }

  async getMember(tenantId: string, householdId: string, memberId: string): Promise<HouseholdMember | null> {
    const r = await $fetch<MemberApiResponse>(
      `/api/proxy/tenants/${tenantId}/households/${householdId}/members/${memberId}`,
    )
    return r.entity ?? null
  }

  async getDietaryProfile(tenantId: string, householdId: string, memberId: string): Promise<DietaryProfile | null> {
    try {
      const r = await $fetch<DietaryProfileApiResponse>(
        `/api/proxy/tenants/${tenantId}/households/${householdId}/members/${memberId}/dietary-profile`,
      )
      return r.entity ?? null
    }
    catch {
      return null
    }
  }
}
