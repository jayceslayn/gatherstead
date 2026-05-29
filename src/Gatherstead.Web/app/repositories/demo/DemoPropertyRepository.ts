import type { IPropertyRepository } from '../interfaces'
import type { PropertySummary, AttributeWriteEntry, AttributeEntry } from '../types'
import { getDemoStore, persistDemoStore, demoId, DEMO_LIMITS, DemoLimitError } from './DemoStore'

function toAttributeEntries(writes: AttributeWriteEntry[] | null | undefined): AttributeEntry[] {
  if (!writes) return []
  return writes.map(w => ({ id: demoId(), key: w.key, value: w.value, tenantMinRole: w.tenantMinRole, householdMinRole: w.householdMinRole ?? null }))
}

export class DemoPropertyRepository implements IPropertyRepository {
  async listProperties(tenantId: string): Promise<PropertySummary[]> {
    return getDemoStore().properties.value.filter(p => p.tenantId === tenantId)
  }

  async getProperty(tenantId: string, propertyId: string): Promise<PropertySummary | null> {
    return getDemoStore().properties.value.find(
      p => p.tenantId === tenantId && p.id === propertyId,
    ) ?? null
  }

  async createProperty(tenantId: string, name: string, notes?: string | null, attributes?: AttributeWriteEntry[] | null): Promise<PropertySummary> {
    const store = getDemoStore()
    if (store.properties.value.filter(p => p.tenantId === tenantId).length >= DEMO_LIMITS.propertiesPerTenant) {
      throw new DemoLimitError('propertiesPerTenant')
    }
    const p: PropertySummary = { id: demoId(), tenantId, name, notes: notes ?? null, attributes: toAttributeEntries(attributes) }
    store.properties.value.push(p)
    persistDemoStore()
    return p
  }

  async updateProperty(_tenantId: string, propertyId: string, name: string, notes?: string | null, attributes?: AttributeWriteEntry[] | null): Promise<void> {
    const store = getDemoStore()
    const p = store.properties.value.find(x => x.id === propertyId)
    if (!p) return
    p.name = name
    p.notes = notes ?? null
    if (attributes !== undefined) p.attributes = toAttributeEntries(attributes)
    persistDemoStore()
  }

  async deleteProperty(_tenantId: string, propertyId: string): Promise<void> {
    const store = getDemoStore()
    const accomIds = store.accommodations.value.filter(a => a.propertyId === propertyId).map(a => a.id)
    store.accommodationIntents.value = store.accommodationIntents.value.filter(i => !accomIds.includes(i.accommodationId))
    store.accommodations.value = store.accommodations.value.filter(a => a.propertyId !== propertyId)
    store.properties.value = store.properties.value.filter(p => p.id !== propertyId)
    persistDemoStore()
  }
}
