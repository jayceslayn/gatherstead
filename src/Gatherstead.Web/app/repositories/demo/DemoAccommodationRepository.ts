import type { IAccommodationRepository, AccommodationAvailabilityQuery } from '../interfaces'
import type { AccommodationSummary, AccommodationType, AccommodationAvailability, MyStay, AttributeWriteEntry, AttributeEntry } from '../types'
import { getDemoStore, persistDemoStore, demoId, DEMO_LIMITS, DemoLimitError } from './DemoStore'

function toAttributeEntries(writes: AttributeWriteEntry[] | null | undefined): AttributeEntry[] {
  if (!writes) return []
  return writes.map(w => ({ id: demoId(), key: w.key, value: w.value, tenantMinRole: w.tenantMinRole, householdMinRole: w.householdMinRole ?? null }))
}

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

  async searchAvailability(tenantId: string, query: AccommodationAvailabilityQuery): Promise<AccommodationAvailability[]> {
    const store = getDemoStore()
    const propertyName = (id: string) => store.properties.value.find(p => p.id === id)?.name ?? ''
    const requestedAdults = query.partyAdults ?? 0
    const requestedChildren = query.partyChildren ?? 0

    const results = store.accommodations.value
      .filter(a => a.tenantId === tenantId)
      .map((a): AccommodationAvailability => {
        // Two night spans overlap when each starts on or before the other ends.
        const overlapping = store.accommodationIntents.value.filter(
          i => i.accommodationId === a.id && i.startNight <= query.endNight && i.endNight >= query.startNight,
        )
        const claimedAdults = overlapping.reduce((s, i) => s + (i.partyAdults ?? 0), 0)
        const claimedChildren = overlapping.reduce((s, i) => s + (i.partyChildren ?? 0), 0)
        const remainingAdults = a.capacityAdults != null ? a.capacityAdults - claimedAdults : null
        const remainingChildren = a.capacityChildren != null ? a.capacityChildren - claimedChildren : null
        const adultsOk = remainingAdults == null || remainingAdults >= requestedAdults
        const childrenOk = remainingChildren == null || remainingChildren >= requestedChildren
        return {
          id: a.id,
          tenantId,
          propertyId: a.propertyId,
          propertyName: propertyName(a.propertyId),
          name: a.name,
          type: a.type,
          notes: a.notes,
          capacityAdults: a.capacityAdults,
          capacityChildren: a.capacityChildren,
          claimedAdults,
          claimedChildren,
          remainingAdults,
          remainingChildren,
          hasSufficientCapacity: adultsOk && childrenOk,
        }
      })
      .filter(r => !query.requireCapacity || r.hasSufficientCapacity)

    return results.sort((x, y) =>
      Number(y.hasSufficientCapacity) - Number(x.hasSufficientCapacity)
      || x.propertyName.localeCompare(y.propertyName)
      || x.name.localeCompare(y.name))
  }

  async listMyStays(tenantId: string, memberId: string, fromNight: string): Promise<MyStay[]> {
    const store = getDemoStore()
    const accommodation = (id: string) => store.accommodations.value.find(a => a.id === id)
    const propertyName = (id: string) => store.properties.value.find(p => p.id === id)?.name ?? ''
    return store.accommodationIntents.value
      .filter(i => i.tenantId === tenantId && i.householdMemberId === memberId && i.endNight >= fromNight)
      .map((i): MyStay => {
        const a = accommodation(i.accommodationId)
        return {
          id: i.id,
          accommodationId: i.accommodationId,
          accommodationName: a?.name ?? '',
          propertyId: a?.propertyId ?? '',
          propertyName: a ? propertyName(a.propertyId) : '',
          householdMemberId: i.householdMemberId,
          startNight: i.startNight,
          endNight: i.endNight,
          status: i.status,
          decision: i.decision,
          partyAdults: i.partyAdults,
          partyChildren: i.partyChildren,
        }
      })
      .sort((x, y) => x.startNight.localeCompare(y.startNight) || x.endNight.localeCompare(y.endNight))
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
    const store = getDemoStore()
    if (store.accommodations.value.filter(a => a.propertyId === propertyId).length >= DEMO_LIMITS.accommodationsPerProperty) {
      throw new DemoLimitError('accommodationsPerProperty')
    }
    const a: AccommodationSummary = { id: demoId(), tenantId, propertyId, name, type, capacityAdults, capacityChildren, notes, attributes: toAttributeEntries(attributes) }
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
    attributes?: AttributeWriteEntry[] | null,
  ): Promise<void> {
    const store = getDemoStore()
    const a = store.accommodations.value.find(x => x.id === accommodationId)
    if (!a) return
    a.name = name
    a.type = type
    a.capacityAdults = capacityAdults
    a.capacityChildren = capacityChildren
    a.notes = notes
    if (attributes !== undefined) a.attributes = toAttributeEntries(attributes)
    persistDemoStore()
  }

  async deleteAccommodation(_tenantId: string, _propertyId: string, accommodationId: string): Promise<void> {
    const store = getDemoStore()
    store.accommodationIntents.value = store.accommodationIntents.value.filter(i => i.accommodationId !== accommodationId)
    store.accommodations.value = store.accommodations.value.filter(a => a.id !== accommodationId)
    persistDemoStore()
  }
}
