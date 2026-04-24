import type { IAccommodationRepository } from '../interfaces'
import type { AccommodationSummary } from '../types'
import { getDemoStore } from './DemoStore'

export class DemoAccommodationRepository implements IAccommodationRepository {
  async listAccommodations(tenantId: string, propertyId: string): Promise<AccommodationSummary[]> {
    return getDemoStore().accommodations.value.filter(
      a => a.tenantId === tenantId && a.propertyId === propertyId,
    )
  }

  async getAccommodation(
    tenantId: string,
    propertyId: string,
    accommodationId: string,
  ): Promise<AccommodationSummary | null> {
    return getDemoStore().accommodations.value.find(
      a => a.tenantId === tenantId && a.propertyId === propertyId && a.id === accommodationId,
    ) ?? null
  }
}
