import type { IHouseholdAttributeRepository } from '../interfaces'
import type { HouseholdAttribute } from '../types'

interface ApiResponse<T> { entity: T; successful: boolean }

export class LiveHouseholdAttributeRepository implements IHouseholdAttributeRepository {
  async listAttributes(tenantId: string, householdId: string): Promise<HouseholdAttribute[]> {
    const r = await $fetch<ApiResponse<HouseholdAttribute[]>>(
      `/api/proxy/tenants/${tenantId}/households/${householdId}/attributes`,
    )
    return r.entity ?? []
  }

  async getAttribute(tenantId: string, householdId: string, attributeId: string): Promise<HouseholdAttribute | null> {
    const r = await $fetch<ApiResponse<HouseholdAttribute>>(
      `/api/proxy/tenants/${tenantId}/households/${householdId}/attributes/${attributeId}`,
    )
    return r.entity ?? null
  }

  async createAttribute(tenantId: string, householdId: string, key: string, value: string, tenantMinRole: number, householdMinRole: number | null): Promise<HouseholdAttribute> {
    const r = await $fetch<ApiResponse<HouseholdAttribute>>(
      `/api/proxy/tenants/${tenantId}/households/${householdId}/attributes`,
      { method: 'POST', body: { key, value, tenantMinRole, householdMinRole } },
    )
    return r.entity
  }

  async updateAttribute(tenantId: string, householdId: string, attributeId: string, key: string, value: string, tenantMinRole: number, householdMinRole: number | null): Promise<void> {
    await $fetch(`/api/proxy/tenants/${tenantId}/households/${householdId}/attributes/${attributeId}`, {
      method: 'PUT',
      body: { key, value, tenantMinRole, householdMinRole },
    })
  }

  async deleteAttribute(tenantId: string, householdId: string, attributeId: string): Promise<void> {
    await $fetch(`/api/proxy/tenants/${tenantId}/households/${householdId}/attributes/${attributeId}`, { method: 'DELETE' })
  }
}
