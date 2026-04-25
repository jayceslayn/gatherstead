import type { IHouseholdMemberRepository } from '../interfaces'
import type { HouseholdMember, HouseholdRole, DietaryProfile } from '../types'

interface ApiResponse<T> { entity: T; successful: boolean }

export class LiveHouseholdMemberRepository implements IHouseholdMemberRepository {
  async listMembers(tenantId: string, householdId: string): Promise<HouseholdMember[]> {
    const r = await $fetch<ApiResponse<HouseholdMember[]>>(
      `/api/proxy/tenants/${tenantId}/households/${householdId}/members`,
    )
    return r.entity ?? []
  }

  async getMember(tenantId: string, householdId: string, memberId: string): Promise<HouseholdMember | null> {
    const r = await $fetch<ApiResponse<HouseholdMember>>(
      `/api/proxy/tenants/${tenantId}/households/${householdId}/members/${memberId}`,
    )
    return r.entity ?? null
  }

  async getDietaryProfile(tenantId: string, householdId: string, memberId: string): Promise<DietaryProfile | null> {
    try {
      const r = await $fetch<ApiResponse<DietaryProfile>>(
        `/api/proxy/tenants/${tenantId}/households/${householdId}/members/${memberId}/dietary-profile`,
      )
      return r.entity ?? null
    }
    catch {
      return null
    }
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
    const r = await $fetch<ApiResponse<HouseholdMember>>(
      `/api/proxy/tenants/${tenantId}/households/${householdId}/members`,
      { method: 'POST', body: { name, isAdult, ageBand, birthDate, householdRole, dietaryNotes, dietaryTags } },
    )
    return r.entity
  }

  async updateMember(
    tenantId: string,
    householdId: string,
    memberId: string,
    name: string,
    isAdult: boolean,
    ageBand: string | null,
    birthDate: string | null,
    householdRole: HouseholdRole,
    dietaryNotes: string | null,
    dietaryTags: string[],
  ): Promise<void> {
    await $fetch(`/api/proxy/tenants/${tenantId}/households/${householdId}/members/${memberId}`, {
      method: 'PUT',
      body: { name, isAdult, ageBand, birthDate, householdRole, dietaryNotes, dietaryTags },
    })
  }

  async deleteMember(tenantId: string, householdId: string, memberId: string): Promise<void> {
    await $fetch(
      `/api/proxy/tenants/${tenantId}/households/${householdId}/members/${memberId}`,
      { method: 'DELETE' },
    )
  }
}
