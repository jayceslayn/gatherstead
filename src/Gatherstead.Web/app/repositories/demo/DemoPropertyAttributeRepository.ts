import type { IPropertyAttributeRepository } from '../interfaces'
import type { PropertyAttribute } from '../types'
import { getDemoStore, persistDemoStore, demoId } from './DemoStore'

export class DemoPropertyAttributeRepository implements IPropertyAttributeRepository {
  async listAttributes(tenantId: string, propertyId: string): Promise<PropertyAttribute[]> {
    return getDemoStore().propertyAttributes.value.filter(
      a => a.tenantId === tenantId && a.parentId === propertyId,
    )
  }

  async getAttribute(tenantId: string, propertyId: string, attributeId: string): Promise<PropertyAttribute | null> {
    return getDemoStore().propertyAttributes.value.find(
      a => a.tenantId === tenantId && a.parentId === propertyId && a.id === attributeId,
    ) ?? null
  }

  async createAttribute(tenantId: string, propertyId: string, key: string, value: string, tenantMinRole: number): Promise<PropertyAttribute> {
    const store = getDemoStore()
    const a: PropertyAttribute = { id: demoId(), tenantId, parentId: propertyId, key, value, tenantMinRole, householdMinRole: null }
    store.propertyAttributes.value.push(a)
    persistDemoStore()
    return a
  }

  async updateAttribute(tenantId: string, propertyId: string, attributeId: string, key: string, value: string, tenantMinRole: number): Promise<void> {
    const store = getDemoStore()
    const a = store.propertyAttributes.value.find(
      x => x.tenantId === tenantId && x.parentId === propertyId && x.id === attributeId,
    )
    if (!a) return
    a.key = key
    a.value = value
    a.tenantMinRole = tenantMinRole
    persistDemoStore()
  }

  async deleteAttribute(tenantId: string, propertyId: string, attributeId: string): Promise<void> {
    const store = getDemoStore()
    store.propertyAttributes.value = store.propertyAttributes.value.filter(
      a => !(a.tenantId === tenantId && a.parentId === propertyId && a.id === attributeId),
    )
    persistDemoStore()
  }
}
