import type { IHouseholdRepository } from '../interfaces'
import type { HouseholdSummary } from '../types'

interface ApiResponse<T> { entity: T; successful: boolean }

export class LiveHouseholdRepository implements IHouseholdRepository {
  async listHouseholds(tenantId: string): Promise<HouseholdSummary[]> {
    const r = await $fetch<ApiResponse<HouseholdSummary[]>>(`/api/proxy/tenants/${tenantId}/households`)
    return r.entity ?? []
  }

  async getHousehold(tenantId: string, householdId: string): Promise<HouseholdSummary | null> {
    const r = await $fetch<ApiResponse<HouseholdSummary>>(
      `/api/proxy/tenants/${tenantId}/households/${householdId}`,
    )
    return r.entity ?? null
  }

  async createHousehold(tenantId: string, name: string): Promise<HouseholdSummary> {
    const r = await $fetch<ApiResponse<HouseholdSummary>>(
      `/api/proxy/tenants/${tenantId}/households`,
      { method: 'POST', body: { name } },
    )
    return r.entity
  }

  async updateHousehold(tenantId: string, householdId: string, name: string): Promise<void> {
    await $fetch(`/api/proxy/tenants/${tenantId}/households/${householdId}`, {
      method: 'PUT',
      body: { name },
    })
  }

  async deleteHousehold(tenantId: string, householdId: string): Promise<void> {
    await $fetch(`/api/proxy/tenants/${tenantId}/households/${householdId}`, { method: 'DELETE' })
  }
}
