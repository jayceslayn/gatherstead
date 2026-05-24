import type { IEquipmentRepository } from '../interfaces'
import type { EquipmentSummary } from '../types'
import { getDemoStore, persistDemoStore, demoId, DEMO_LIMITS, DemoLimitError } from './DemoStore'

export class DemoEquipmentRepository implements IEquipmentRepository {
  async listEquipment(tenantId: string): Promise<EquipmentSummary[]> {
    return getDemoStore().equipment.value.filter(e => e.tenantId === tenantId)
  }

  async getEquipment(tenantId: string, equipmentId: string): Promise<EquipmentSummary | null> {
    return getDemoStore().equipment.value.find(
      e => e.tenantId === tenantId && e.id === equipmentId,
    ) ?? null
  }

  async createEquipment(tenantId: string, name: string, propertyId: string | null, notes: string | null): Promise<EquipmentSummary> {
    const store = getDemoStore()
    if (store.equipment.value.filter(e => e.tenantId === tenantId).length >= DEMO_LIMITS.equipmentPerTenant) {
      throw new DemoLimitError('equipmentPerTenant')
    }
    const e: EquipmentSummary = { id: demoId(), tenantId, propertyId, name, notes }
    store.equipment.value.push(e)
    persistDemoStore()
    return e
  }

  async updateEquipment(_tenantId: string, equipmentId: string, name: string, propertyId: string | null, notes: string | null): Promise<void> {
    const store = getDemoStore()
    const e = store.equipment.value.find(x => x.id === equipmentId)
    if (!e) return
    e.name = name
    e.propertyId = propertyId
    e.notes = notes
    persistDemoStore()
  }

  async deleteEquipment(_tenantId: string, equipmentId: string): Promise<void> {
    const store = getDemoStore()
    store.equipmentAttributes.value = store.equipmentAttributes.value.filter(a => a.parentId !== equipmentId)
    store.equipment.value = store.equipment.value.filter(e => e.id !== equipmentId)
    persistDemoStore()
  }
}
