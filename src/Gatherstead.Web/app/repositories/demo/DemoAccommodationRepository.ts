import type { IAccommodationRepository, AccommodationAvailabilityQuery, AccommodationDimensions } from '../interfaces'
import type { AccommodationSummary, AccommodationType, AccommodationAvailability, MyStay, AttributeWriteEntry, AttributeEntry, Bed, BedWriteEntry } from '../types'
import { getDemoStore, persistDemoStore, demoId, DEMO_LIMITS, DemoLimitError } from './DemoStore'
import { sleepsCapacity } from './DemoHelpers'
import { effectiveAreaSqMeters } from '../../utils/units'

function toAttributeEntries(writes: AttributeWriteEntry[] | null | undefined): AttributeEntry[] {
  if (!writes) return []
  return writes.map(w => ({ id: demoId(), key: w.key, value: w.value, tenantMinRole: w.tenantMinRole, householdMinRole: w.householdMinRole ?? null }))
}

function bedsToEntries(beds: BedWriteEntry[]): Bed[] {
  return beds.filter(b => b.quantity > 0).map(b => ({ id: demoId(), size: b.size, quantity: b.quantity }))
}

// Area override wins over width × depth; null when neither is known.
function effectiveArea(d: AccommodationDimensions): number | null {
  return effectiveAreaSqMeters(d.widthMeters, d.depthMeters, d.areaSqMeters)
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
    const requestedParty = (query.partyAdults ?? 0) + (query.partyChildren ?? 0)
    // Scope to the selected properties when any are given; an empty list spans all properties.
    const propertyIds = query.propertyIds ?? []

    const results = store.accommodations.value
      .filter(a => a.tenantId === tenantId && (propertyIds.length === 0 || propertyIds.includes(a.propertyId)))
      .map((a): AccommodationAvailability => {
        // Two night spans overlap when each starts on or before the other ends. Declined stays free the slot.
        const overlapping = store.accommodationIntents.value.filter(
          i => i.accommodationId === a.id
            && i.status !== 'Declined'
            && i.startNight <= query.endNight && i.endNight >= query.startNight,
        )
        const occupied = overlapping.reduce((s, i) => s + (i.partyAdults ?? 0) + (i.partyChildren ?? 0), 0)
        const capacity = sleepsCapacity(a.beds ?? [])
        const remaining = capacity != null ? capacity - occupied : null
        const hasSufficientCapacity = remaining == null || remaining >= requestedParty
        return {
          id: a.id,
          tenantId,
          propertyId: a.propertyId,
          propertyName: propertyName(a.propertyId),
          name: a.name,
          type: a.type,
          notes: a.notes,
          capacity,
          occupied,
          remaining,
          hasSufficientCapacity,
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
    dimensions: AccommodationDimensions,
    beds: BedWriteEntry[],
    notes: string | null,
    attributes?: AttributeWriteEntry[] | null,
  ): Promise<AccommodationSummary> {
    const store = getDemoStore()
    if (store.accommodations.value.filter(a => a.propertyId === propertyId).length >= DEMO_LIMITS.accommodationsPerProperty) {
      throw new DemoLimitError('accommodationsPerProperty')
    }
    const bedEntries = bedsToEntries(beds)
    const a: AccommodationSummary = {
      id: demoId(), tenantId, propertyId, name, type,
      widthMeters: dimensions.widthMeters,
      depthMeters: dimensions.depthMeters,
      areaSqMeters: dimensions.areaSqMeters,
      effectiveAreaSqMeters: effectiveArea(dimensions),
      notes,
      beds: bedEntries,
      attributes: toAttributeEntries(attributes),
    }
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
    dimensions: AccommodationDimensions,
    beds: BedWriteEntry[],
    notes: string | null,
    attributes?: AttributeWriteEntry[] | null,
  ): Promise<void> {
    const store = getDemoStore()
    const a = store.accommodations.value.find(x => x.id === accommodationId)
    if (!a) return
    a.name = name
    a.type = type
    a.widthMeters = dimensions.widthMeters
    a.depthMeters = dimensions.depthMeters
    a.areaSqMeters = dimensions.areaSqMeters
    a.effectiveAreaSqMeters = effectiveArea(dimensions)
    a.beds = bedsToEntries(beds)
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
