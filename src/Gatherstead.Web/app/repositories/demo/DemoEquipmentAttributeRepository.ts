import type { IEquipmentAttributeRepository } from '../interfaces'
import type { EquipmentAttribute } from '../types'
import { getDemoStore, persistDemoStore, demoId } from './DemoStore'

export class DemoEquipmentAttributeRepository implements IEquipmentAttributeRepository {
  async listAttributes(tenantId: string, equipmentId: string): Promise<EquipmentAttribute[]> {
    return getDemoStore().equipmentAttributes.value.filter(
      a => a.tenantId === tenantId && a.parentId === equipmentId,
    )
  }

  async getAttribute(tenantId: string, equipmentId: string, attributeId: string): Promise<EquipmentAttribute | null> {
    return getDemoStore().equipmentAttributes.value.find(
      a => a.tenantId === tenantId && a.parentId === equipmentId && a.id === attributeId,
    ) ?? null
  }

  async createAttribute(tenantId: string, equipmentId: string, key: string, value: string, tenantMinRole: number): Promise<EquipmentAttribute> {
    const store = getDemoStore()
    const a: EquipmentAttribute = { id: demoId(), tenantId, parentId: equipmentId, key, value, tenantMinRole, householdMinRole: null }
    store.equipmentAttributes.value.push(a)
    persistDemoStore()
    return a
  }

  async updateAttribute(tenantId: string, equipmentId: string, attributeId: string, key: string, value: string, tenantMinRole: number): Promise<void> {
    const store = getDemoStore()
    const a = store.equipmentAttributes.value.find(
      x => x.tenantId === tenantId && x.parentId === equipmentId && x.id === attributeId,
    )
    if (!a) return
    a.key = key
    a.value = value
    a.tenantMinRole = tenantMinRole
    persistDemoStore()
  }

  async deleteAttribute(tenantId: string, equipmentId: string, attributeId: string): Promise<void> {
    const store = getDemoStore()
    store.equipmentAttributes.value = store.equipmentAttributes.value.filter(
      a => !(a.tenantId === tenantId && a.parentId === equipmentId && a.id === attributeId),
    )
    persistDemoStore()
  }
}
