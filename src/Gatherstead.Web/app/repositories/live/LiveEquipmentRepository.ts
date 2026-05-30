import type { IEquipmentRepository } from '../interfaces'
import type { EquipmentSummary, AttributeWriteEntry } from '../types'

interface ApiResponse<T> { entity: T; successful: boolean }

export class LiveEquipmentRepository implements IEquipmentRepository {
  async listEquipment(tenantId: string): Promise<EquipmentSummary[]> {
    const r = await $fetch<ApiResponse<EquipmentSummary[]>>(
      `/api/proxy/tenants/${tenantId}/equipment`,
    )
    return r.entity ?? []
  }

  async getEquipment(tenantId: string, equipmentId: string): Promise<EquipmentSummary | null> {
    const r = await $fetch<ApiResponse<EquipmentSummary>>(
      `/api/proxy/tenants/${tenantId}/equipment/${equipmentId}`,
    )
    return r.entity ?? null
  }

  async createEquipment(tenantId: string, name: string, propertyId: string | null, notes: string | null, attributes?: AttributeWriteEntry[] | null): Promise<EquipmentSummary> {
    const r = await $fetch<ApiResponse<EquipmentSummary>>(
      `/api/proxy/tenants/${tenantId}/equipment`,
      { method: 'POST', body: { name, propertyId, notes, attributes: attributes ?? null } },
    )
    return r.entity
  }

  async updateEquipment(tenantId: string, equipmentId: string, name: string, propertyId: string | null, notes: string | null, attributes?: AttributeWriteEntry[] | null): Promise<void> {
    await $fetch(`/api/proxy/tenants/${tenantId}/equipment/${equipmentId}`, {
      method: 'PUT',
      body: { name, propertyId, notes, attributes: attributes ?? null },
    })
  }

  async deleteEquipment(tenantId: string, equipmentId: string): Promise<void> {
    await $fetch(`/api/proxy/tenants/${tenantId}/equipment/${equipmentId}`, { method: 'DELETE' })
  }
}
