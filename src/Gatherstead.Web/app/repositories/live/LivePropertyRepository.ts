import type { IPropertyRepository } from '../interfaces'
import type { PropertySummary } from '../types'

interface ApiResponse<T> { entity: T; successful: boolean }

export class LivePropertyRepository implements IPropertyRepository {
  async listProperties(tenantId: string): Promise<PropertySummary[]> {
    const r = await $fetch<ApiResponse<PropertySummary[]>>(
      `/api/proxy/tenants/${tenantId}/properties`,
    )
    return r.entity ?? []
  }

  async getProperty(tenantId: string, propertyId: string): Promise<PropertySummary | null> {
    const r = await $fetch<ApiResponse<PropertySummary>>(
      `/api/proxy/tenants/${tenantId}/properties/${propertyId}`,
    )
    return r.entity ?? null
  }
}
