import type { IPropertyAttributeRepository } from '../interfaces'
import type { PropertyAttribute } from '../types'

interface ApiResponse<T> { entity: T; successful: boolean }

export class LivePropertyAttributeRepository implements IPropertyAttributeRepository {
  async listAttributes(tenantId: string, propertyId: string): Promise<PropertyAttribute[]> {
    const r = await $fetch<ApiResponse<PropertyAttribute[]>>(
      `/api/proxy/tenants/${tenantId}/properties/${propertyId}/attributes`,
    )
    return r.entity ?? []
  }

  async getAttribute(tenantId: string, propertyId: string, attributeId: string): Promise<PropertyAttribute | null> {
    const r = await $fetch<ApiResponse<PropertyAttribute>>(
      `/api/proxy/tenants/${tenantId}/properties/${propertyId}/attributes/${attributeId}`,
    )
    return r.entity ?? null
  }

  async createAttribute(tenantId: string, propertyId: string, key: string, value: string, tenantMinRole: number): Promise<PropertyAttribute> {
    const r = await $fetch<ApiResponse<PropertyAttribute>>(
      `/api/proxy/tenants/${tenantId}/properties/${propertyId}/attributes`,
      { method: 'POST', body: { key, value, tenantMinRole } },
    )
    return r.entity
  }

  async updateAttribute(tenantId: string, propertyId: string, attributeId: string, key: string, value: string, tenantMinRole: number): Promise<void> {
    await $fetch(`/api/proxy/tenants/${tenantId}/properties/${propertyId}/attributes/${attributeId}`, {
      method: 'PUT',
      body: { key, value, tenantMinRole },
    })
  }

  async deleteAttribute(tenantId: string, propertyId: string, attributeId: string): Promise<void> {
    await $fetch(`/api/proxy/tenants/${tenantId}/properties/${propertyId}/attributes/${attributeId}`, { method: 'DELETE' })
  }
}
