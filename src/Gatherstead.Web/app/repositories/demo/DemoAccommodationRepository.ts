import type { IAccommodationRepository } from '../interfaces'
import type { AccommodationSummary, AccommodationType } from '../types'
import { getDemoStore, persistDemoStore, demoId, DEMO_LIMITS, DemoLimitError } from './DemoStore'

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

  async createAccommodation(
    tenantId: string,
    propertyId: string,
    name: string,
    type: AccommodationType,
    capacityAdults: number | null,
    capacityChildren: number | null,
    notes: string | null,
  ): Promise<AccommodationSummary> {
    const store = getDemoStore()
    if (store.accommodations.value.filter(a => a.propertyId === propertyId).length >= DEMO_LIMITS.accommodationsPerProperty) {
      throw new DemoLimitError('accommodationsPerProperty')
    }
    const a: AccommodationSummary = { id: demoId(), tenantId, propertyId, name, type, capacityAdults, capacityChildren, notes }
    store.accommodations.value.push(a)
    persistDemoStore()
    return a
  }

  async updateAccommodation(
    _tenantId: string,
    _propertyId: string,
    accommodationId: string,
    name: string,
    type: AccommodationType,
    capacityAdults: number | null,
    capacityChildren: number | null,
    notes: string | null,
  ): Promise<void> {
    const store = getDemoStore()
    const a = store.accommodations.value.find(x => x.id === accommodationId)
    if (!a) return
    a.name = name
    a.type = type
    a.capacityAdults = capacityAdults
    a.capacityChildren = capacityChildren
    a.notes = notes
    persistDemoStore()
  }

  async deleteAccommodation(_tenantId: string, _propertyId: string, accommodationId: string): Promise<void> {
    const store = getDemoStore()
    store.accommodationIntents.value = store.accommodationIntents.value.filter(i => i.accommodationId !== accommodationId)
    store.accommodations.value = store.accommodations.value.filter(a => a.id !== accommodationId)
    persistDemoStore()
  }
}
