import type { IAccommodationAttributeRepository } from '../interfaces'
import type { AccommodationAttribute } from '../types'

interface ApiResponse<T> { entity: T; successful: boolean }

export class LiveAccommodationAttributeRepository implements IAccommodationAttributeRepository {
  async listAttributes(tenantId: string, accommodationId: string): Promise<AccommodationAttribute[]> {
    const r = await $fetch<ApiResponse<AccommodationAttribute[]>>(
      `/api/proxy/tenants/${tenantId}/accommodations/${accommodationId}/attributes`,
    )
    return r.entity ?? []
  }

  async getAttribute(tenantId: string, accommodationId: string, attributeId: string): Promise<AccommodationAttribute | null> {
    const r = await $fetch<ApiResponse<AccommodationAttribute>>(
      `/api/proxy/tenants/${tenantId}/accommodations/${accommodationId}/attributes/${attributeId}`,
    )
    return r.entity ?? null
  }

  async createAttribute(tenantId: string, accommodationId: string, key: string, value: string, tenantMinRole: number): Promise<AccommodationAttribute> {
    const r = await $fetch<ApiResponse<AccommodationAttribute>>(
      `/api/proxy/tenants/${tenantId}/accommodations/${accommodationId}/attributes`,
      { method: 'POST', body: { key, value, tenantMinRole } },
    )
    return r.entity
  }

  async updateAttribute(tenantId: string, accommodationId: string, attributeId: string, key: string, value: string, tenantMinRole: number): Promise<void> {
    await $fetch(`/api/proxy/tenants/${tenantId}/accommodations/${accommodationId}/attributes/${attributeId}`, {
      method: 'PUT',
      body: { key, value, tenantMinRole },
    })
  }

  async deleteAttribute(tenantId: string, accommodationId: string, attributeId: string): Promise<void> {
    await $fetch(`/api/proxy/tenants/${tenantId}/accommodations/${accommodationId}/attributes/${attributeId}`, { method: 'DELETE' })
  }
}
