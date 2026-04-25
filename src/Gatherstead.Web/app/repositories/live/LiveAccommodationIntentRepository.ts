import type { IAccommodationIntentRepository } from '../interfaces'
import type {
  AccommodationIntent,
  AccommodationIntentStatus,
  AccommodationIntentDecision,
} from '../types'

interface ApiResponse<T> { entity: T; successful: boolean }

export class LiveAccommodationIntentRepository implements IAccommodationIntentRepository {
  async listIntents(
    tenantId: string,
    propertyId: string,
    accommodationId: string,
  ): Promise<AccommodationIntent[]> {
    const r = await $fetch<ApiResponse<AccommodationIntent[]>>(
      `/api/proxy/tenants/${tenantId}/properties/${propertyId}/accommodations/${accommodationId}/intents`,
    )
    return r.entity ?? []
  }

  async listIntentsForMember(
    tenantId: string,
    propertyId: string,
    accommodationId: string,
    memberId: string,
  ): Promise<AccommodationIntent[]> {
    const r = await $fetch<ApiResponse<AccommodationIntent[]>>(
      `/api/proxy/tenants/${tenantId}/properties/${propertyId}/accommodations/${accommodationId}/intents?memberIds=${encodeURIComponent(memberId)}`,
    )
    return r.entity ?? []
  }

  async createIntent(
    tenantId: string,
    propertyId: string,
    accommodationId: string,
    householdId: string,
    memberId: string,
    night: string,
    status: AccommodationIntentStatus,
    notes?: string | null,
    partySize?: number | null,
  ): Promise<AccommodationIntent> {
    const r = await $fetch<ApiResponse<AccommodationIntent>>(
      `/api/proxy/tenants/${tenantId}/properties/${propertyId}/accommodations/${accommodationId}/intents?householdId=${encodeURIComponent(householdId)}`,
      {
        method: 'POST',
        body: { householdMemberId: memberId, night, status, notes, partySize },
      },
    )
    return r.entity
  }

  async updateIntent(
    tenantId: string,
    propertyId: string,
    accommodationId: string,
    intentId: string,
    status: AccommodationIntentStatus,
    decision: AccommodationIntentDecision,
    notes?: string | null,
    partySize?: number | null,
  ): Promise<void> {
    await $fetch(
      `/api/proxy/tenants/${tenantId}/properties/${propertyId}/accommodations/${accommodationId}/intents/${intentId}`,
      {
        method: 'PUT',
        body: { status, decision, notes, partySize },
      },
    )
  }

  async deleteIntent(
    tenantId: string,
    propertyId: string,
    accommodationId: string,
    intentId: string,
  ): Promise<void> {
    await $fetch(
      `/api/proxy/tenants/${tenantId}/properties/${propertyId}/accommodations/${accommodationId}/intents/${intentId}`,
      { method: 'DELETE' },
    )
  }
}
