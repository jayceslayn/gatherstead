import type { IHouseholdAttributeRepository } from '../interfaces'
import type { HouseholdAttribute } from '../types'
import { getDemoStore, persistDemoStore, demoId } from './DemoStore'

export class DemoHouseholdAttributeRepository implements IHouseholdAttributeRepository {
  async listAttributes(tenantId: string, householdId: string): Promise<HouseholdAttribute[]> {
    return getDemoStore().householdAttributes.value.filter(
      a => a.tenantId === tenantId && a.parentId === householdId,
    )
  }

  async getAttribute(tenantId: string, householdId: string, attributeId: string): Promise<HouseholdAttribute | null> {
    return getDemoStore().householdAttributes.value.find(
      a => a.tenantId === tenantId && a.parentId === householdId && a.id === attributeId,
    ) ?? null
  }

  async createAttribute(tenantId: string, householdId: string, key: string, value: string, tenantMinRole: number, householdMinRole: number | null): Promise<HouseholdAttribute> {
    const store = getDemoStore()
    const a: HouseholdAttribute = { id: demoId(), tenantId, parentId: householdId, key, value, tenantMinRole, householdMinRole }
    store.householdAttributes.value.push(a)
    persistDemoStore()
    return a
  }

  async updateAttribute(tenantId: string, householdId: string, attributeId: string, key: string, value: string, tenantMinRole: number, householdMinRole: number | null): Promise<void> {
    const store = getDemoStore()
    const a = store.householdAttributes.value.find(
      x => x.tenantId === tenantId && x.parentId === householdId && x.id === attributeId,
    )
    if (!a) return
    a.key = key
    a.value = value
    a.tenantMinRole = tenantMinRole
    a.householdMinRole = householdMinRole
    persistDemoStore()
  }

  async deleteAttribute(tenantId: string, householdId: string, attributeId: string): Promise<void> {
    const store = getDemoStore()
    store.householdAttributes.value = store.householdAttributes.value.filter(
      a => !(a.tenantId === tenantId && a.parentId === householdId && a.id === attributeId),
    )
    persistDemoStore()
  }
}
