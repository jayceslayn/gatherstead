import type { IAccommodationAttributeRepository } from '../interfaces'
import type { AccommodationAttribute } from '../types'
import { getDemoStore, persistDemoStore, demoId } from './DemoStore'

export class DemoAccommodationAttributeRepository implements IAccommodationAttributeRepository {
  async listAttributes(tenantId: string, accommodationId: string): Promise<AccommodationAttribute[]> {
    return getDemoStore().accommodationAttributes.value.filter(
      a => a.tenantId === tenantId && a.parentId === accommodationId,
    )
  }

  async getAttribute(tenantId: string, accommodationId: string, attributeId: string): Promise<AccommodationAttribute | null> {
    return getDemoStore().accommodationAttributes.value.find(
      a => a.tenantId === tenantId && a.parentId === accommodationId && a.id === attributeId,
    ) ?? null
  }

  async createAttribute(tenantId: string, accommodationId: string, key: string, value: string, tenantMinRole: number): Promise<AccommodationAttribute> {
    const store = getDemoStore()
    const a: AccommodationAttribute = { id: demoId(), tenantId, parentId: accommodationId, key, value, tenantMinRole, householdMinRole: null }
    store.accommodationAttributes.value.push(a)
    persistDemoStore()
    return a
  }

  async updateAttribute(tenantId: string, accommodationId: string, attributeId: string, key: string, value: string, tenantMinRole: number): Promise<void> {
    const store = getDemoStore()
    const a = store.accommodationAttributes.value.find(
      x => x.tenantId === tenantId && x.parentId === accommodationId && x.id === attributeId,
    )
    if (!a) return
    a.key = key
    a.value = value
    a.tenantMinRole = tenantMinRole
    persistDemoStore()
  }

  async deleteAttribute(tenantId: string, accommodationId: string, attributeId: string): Promise<void> {
    const store = getDemoStore()
    store.accommodationAttributes.value = store.accommodationAttributes.value.filter(
      a => !(a.tenantId === tenantId && a.parentId === accommodationId && a.id === attributeId),
    )
    persistDemoStore()
  }
}
