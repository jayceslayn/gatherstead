import type { IAccommodationRepository } from '../interfaces'
import type { AccommodationSummary, AccommodationType, AttributeWriteEntry } from '../types'

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

  async createAccommodation(
    tenantId: string,
    propertyId: string,
    name: string,
    type: AccommodationType,
    capacityAdults: number | null,
    capacityChildren: number | null,
    notes: string | null,
    attributes?: AttributeWriteEntry[] | null,
  ): Promise<AccommodationSummary> {
    const r = await $fetch<ApiResponse<AccommodationSummary>>(
      `/api/proxy/tenants/${tenantId}/properties/${propertyId}/accommodations`,
      { method: 'POST', body: { name, type, capacityAdults, capacityChildren, notes, attributes: attributes ?? null } },
    )
    return r.entity
  }

  async updateAccommodation(
    tenantId: string,
    propertyId: string,
    accommodationId: string,
    name: string,
    type: AccommodationType,
    capacityAdults: number | null,
    capacityChildren: number | null,
    notes: string | null,
    attributes?: AttributeWriteEntry[] | null,
  ): Promise<void> {
    await $fetch(
      `/api/proxy/tenants/${tenantId}/properties/${propertyId}/accommodations/${accommodationId}`,
      { method: 'PUT', body: { name, type, capacityAdults, capacityChildren, notes, attributes: attributes ?? null } },
    )
  }

  async deleteAccommodation(tenantId: string, propertyId: string, accommodationId: string): Promise<void> {
    await $fetch(
      `/api/proxy/tenants/${tenantId}/properties/${propertyId}/accommodations/${accommodationId}`,
      { method: 'DELETE' },
    )
  }
}
