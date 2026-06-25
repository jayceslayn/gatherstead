import type { IAccommodationRepository, AccommodationAvailabilityQuery } from '../interfaces'
import type { AccommodationSummary, AccommodationType, AccommodationAvailability, MyStay, AttributeWriteEntry } from '../types'

interface ApiResponse<T> { entity: T; successful: boolean }

export class LiveAccommodationRepository implements IAccommodationRepository {
  async listAccommodations(tenantId: string, propertyId: string): Promise<AccommodationSummary[]> {
    const r = await $fetch<ApiResponse<AccommodationSummary[]>>(
      `/api/proxy/tenants/${tenantId}/properties/${propertyId}/accommodations`,
    )
    return r.entity ?? []
  }

  async searchAvailability(tenantId: string, query: AccommodationAvailabilityQuery): Promise<AccommodationAvailability[]> {
    const params = new URLSearchParams({
      startNight: query.startNight,
      endNight: query.endNight,
      requireCapacity: String(query.requireCapacity),
    })
    if (query.partyAdults != null) params.set('partyAdults', String(query.partyAdults))
    if (query.partyChildren != null) params.set('partyChildren', String(query.partyChildren))
    const r = await $fetch<ApiResponse<AccommodationAvailability[]>>(
      `/api/proxy/tenants/${tenantId}/accommodations/availability?${params.toString()}`,
    )
    return r.entity ?? []
  }

  async listMyStays(tenantId: string, memberId: string, fromNight: string): Promise<MyStay[]> {
    const params = new URLSearchParams({ memberIds: memberId, fromNight })
    const r = await $fetch<ApiResponse<MyStay[]>>(
      `/api/proxy/tenants/${tenantId}/accommodation-intents?${params.toString()}`,
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
