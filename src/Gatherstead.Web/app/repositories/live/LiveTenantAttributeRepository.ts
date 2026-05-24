import type { ITenantAttributeRepository } from '../interfaces'
import type { TenantAttribute } from '../types'

interface ApiResponse<T> { entity: T; successful: boolean }

export class LiveTenantAttributeRepository implements ITenantAttributeRepository {
  async listAttributes(tenantId: string): Promise<TenantAttribute[]> {
    const r = await $fetch<ApiResponse<TenantAttribute[]>>(
      `/api/proxy/tenants/${tenantId}/attributes`,
    )
    return r.entity ?? []
  }

  async getAttribute(tenantId: string, attributeId: string): Promise<TenantAttribute | null> {
    const r = await $fetch<ApiResponse<TenantAttribute>>(
      `/api/proxy/tenants/${tenantId}/attributes/${attributeId}`,
    )
    return r.entity ?? null
  }

  async createAttribute(tenantId: string, key: string, value: string, tenantMinRole: number): Promise<TenantAttribute> {
    const r = await $fetch<ApiResponse<TenantAttribute>>(
      `/api/proxy/tenants/${tenantId}/attributes`,
      { method: 'POST', body: { key, value, tenantMinRole } },
    )
    return r.entity
  }

  async updateAttribute(tenantId: string, attributeId: string, key: string, value: string, tenantMinRole: number): Promise<void> {
    await $fetch(`/api/proxy/tenants/${tenantId}/attributes/${attributeId}`, {
      method: 'PUT',
      body: { key, value, tenantMinRole },
    })
  }

  async deleteAttribute(tenantId: string, attributeId: string): Promise<void> {
    await $fetch(`/api/proxy/tenants/${tenantId}/attributes/${attributeId}`, { method: 'DELETE' })
  }
}
