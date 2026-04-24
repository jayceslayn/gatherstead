import type { IAccommodationRepository } from '../interfaces'
import type { AccommodationSummary } from '../types'

interface ApiResponse<T> { entity: T; successful: boolean }

export class LiveAccommodationRepository implements IAccommodationRepository {
  async listAccommodations(tenantId: string, propertyId: string): Promise<AccommodationSummary[]> {
    const r = await $fetch<ApiResponse<AccommodationSummary[]>>(
      `/api/proxy/tenants/${tenantId}/properties/${propertyId}/accommodations`,
    )
    return r.entity ?? []
  }

  async getAccommodation(
    tenantId: string,
    propertyId: string,
    accommodationId: string,
  ): Promise<AccommodationSummary | null> {
    const r = await $fetch<ApiResponse<AccommodationSummary>>(
      `/api/proxy/tenants/${tenantId}/properties/${propertyId}/accommodations/${accommodationId}`,
    )
    return r.entity ?? null
  }
}
