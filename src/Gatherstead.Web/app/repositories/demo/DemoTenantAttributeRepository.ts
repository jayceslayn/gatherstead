import type { ITenantAttributeRepository } from '../interfaces'
import type { TenantAttribute } from '../types'
import { getDemoStore, persistDemoStore, demoId } from './DemoStore'

export class DemoTenantAttributeRepository implements ITenantAttributeRepository {
  async listAttributes(tenantId: string): Promise<TenantAttribute[]> {
    return getDemoStore().tenantAttributes.value.filter(a => a.tenantId === tenantId)
  }

  async getAttribute(tenantId: string, attributeId: string): Promise<TenantAttribute | null> {
    return getDemoStore().tenantAttributes.value.find(
      a => a.tenantId === tenantId && a.id === attributeId,
    ) ?? null
  }

  async createAttribute(tenantId: string, key: string, value: string, tenantMinRole: number): Promise<TenantAttribute> {
    const store = getDemoStore()
    const a: TenantAttribute = { id: demoId(), tenantId, parentId: tenantId, key, value, tenantMinRole, householdMinRole: null }
    store.tenantAttributes.value.push(a)
    persistDemoStore()
    return a
  }

  async updateAttribute(tenantId: string, attributeId: string, key: string, value: string, tenantMinRole: number): Promise<void> {
    const store = getDemoStore()
    const a = store.tenantAttributes.value.find(x => x.tenantId === tenantId && x.id === attributeId)
    if (!a) return
    a.key = key
    a.value = value
    a.tenantMinRole = tenantMinRole
    persistDemoStore()
  }

  async deleteAttribute(tenantId: string, attributeId: string): Promise<void> {
    const store = getDemoStore()
    store.tenantAttributes.value = store.tenantAttributes.value.filter(
      a => !(a.tenantId === tenantId && a.id === attributeId),
    )
    persistDemoStore()
  }
}
