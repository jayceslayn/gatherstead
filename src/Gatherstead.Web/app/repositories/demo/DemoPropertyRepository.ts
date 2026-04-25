import type { IPropertyRepository } from '../interfaces'
import type { PropertySummary } from '../types'
import { getDemoStore, persistDemoStore, demoId, DEMO_LIMITS, DemoLimitError } from './DemoStore'

export class DemoPropertyRepository implements IPropertyRepository {
  async listProperties(tenantId: string): Promise<PropertySummary[]> {
    return getDemoStore().properties.value.filter(p => p.tenantId === tenantId)
  }

  async getProperty(tenantId: string, propertyId: string): Promise<PropertySummary | null> {
    return getDemoStore().properties.value.find(
      p => p.tenantId === tenantId && p.id === propertyId,
    ) ?? null
  }

  async createProperty(tenantId: string, name: string): Promise<PropertySummary> {
    const store = getDemoStore()
    if (store.properties.value.filter(p => p.tenantId === tenantId).length >= DEMO_LIMITS.propertiesPerTenant) {
      throw new DemoLimitError('propertiesPerTenant')
    }
    const p: PropertySummary = { id: demoId(), tenantId, name }
    store.properties.value.push(p)
    persistDemoStore()
    return p
  }

  async updateProperty(_tenantId: string, propertyId: string, name: string): Promise<void> {
    const store = getDemoStore()
    const p = store.properties.value.find(x => x.id === propertyId)
    if (!p) return
    p.name = name
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
