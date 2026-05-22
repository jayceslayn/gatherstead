import type { HouseholdRole, HouseholdUserSummary, TenantRole, TenantUserSummary } from '../types'
import type { ITenantUserRepository } from '../interfaces'

export class LiveTenantUserRepository implements ITenantUserRepository {
  async setLinkedMember(tenantId: string, userId: string, memberId: string | null): Promise<void> {
    await $fetch(
      `/api/proxy/tenants/${tenantId}/users/${userId}/linked-member`,
      { method: 'PUT', body: { memberId } },
    )
  }

  async listTenantUsers(tenantId: string): Promise<TenantUserSummary[]> {
    const response = await $fetch<{ entity: TenantUserSummary[] }>(
      `/api/proxy/tenants/${tenantId}/users`,
    )
    return response.entity ?? []
  }

  async updateRole(tenantId: string, userId: string, role: TenantRole): Promise<void> {
    await $fetch(
      `/api/proxy/tenants/${tenantId}/users/${userId}/role`,
      { method: 'PUT', body: { role } },
    )
  }

  async listHouseholdUsers(tenantId: string, householdId: string): Promise<HouseholdUserSummary[]> {
    const response = await $fetch<{ entity: HouseholdUserSummary[] }>(
      `/api/proxy/tenants/${tenantId}/households/${householdId}/users`,
    )
    return response.entity ?? []
  }

  async upsertHouseholdUser(tenantId: string, householdId: string, userId: string, role: HouseholdRole): Promise<void> {
    await $fetch(
      `/api/proxy/tenants/${tenantId}/households/${householdId}/users/${userId}`,
      { method: 'PUT', body: { role } },
    )
  }

  async deleteHouseholdUser(tenantId: string, householdId: string, userId: string): Promise<void> {
    await $fetch(
      `/api/proxy/tenants/${tenantId}/households/${householdId}/users/${userId}`,
      { method: 'DELETE' },
    )
  }
}
