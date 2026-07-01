import type { IHouseholdMemberRepository } from '../interfaces'
import type { HouseholdMember, AttributeWriteEntry } from '../types'

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

  async createMember(
    tenantId: string,
    householdId: string,
    name: string,
    isAdult: boolean,
    ageBand: string | null,
    birthDate: string | null,
    dietaryNotes: string | null,
    notes: string | null,
    dietaryTags: string[],
    attributes?: AttributeWriteEntry[] | null,
  ): Promise<HouseholdMember> {
    const r = await $fetch<ApiResponse<HouseholdMember>>(
      `/api/proxy/tenants/${tenantId}/households/${householdId}/members`,
      { method: 'POST', body: { name, isAdult, ageBand, birthDate, dietaryNotes, notes, dietaryTags, attributes: attributes ?? null } },
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
    dietaryNotes: string | null,
    notes: string | null,
    dietaryTags: string[],
    attributes?: AttributeWriteEntry[] | null,
  ): Promise<void> {
    await $fetch(`/api/proxy/tenants/${tenantId}/households/${householdId}/members/${memberId}`, {
      method: 'PUT',
      body: { name, isAdult, ageBand, birthDate, dietaryNotes, notes, dietaryTags, attributes: attributes ?? null },
    })
  }

  async deleteMember(tenantId: string, householdId: string, memberId: string): Promise<void> {
    await $fetch(
      `/api/proxy/tenants/${tenantId}/households/${householdId}/members/${memberId}`,
      { method: 'DELETE' },
    )
  }
}
