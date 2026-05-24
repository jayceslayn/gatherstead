import type { IEquipmentAttributeRepository } from '../interfaces'
import type { EquipmentAttribute } from '../types'

interface ApiResponse<T> { entity: T; successful: boolean }

export class LiveEquipmentAttributeRepository implements IEquipmentAttributeRepository {
  async listAttributes(tenantId: string, equipmentId: string): Promise<EquipmentAttribute[]> {
    const r = await $fetch<ApiResponse<EquipmentAttribute[]>>(
      `/api/proxy/tenants/${tenantId}/equipment/${equipmentId}/attributes`,
    )
    return r.entity ?? []
  }

  async getAttribute(tenantId: string, equipmentId: string, attributeId: string): Promise<EquipmentAttribute | null> {
    const r = await $fetch<ApiResponse<EquipmentAttribute>>(
      `/api/proxy/tenants/${tenantId}/equipment/${equipmentId}/attributes/${attributeId}`,
    )
    return r.entity ?? null
  }

  async createAttribute(tenantId: string, equipmentId: string, key: string, value: string, tenantMinRole: number): Promise<EquipmentAttribute> {
    const r = await $fetch<ApiResponse<EquipmentAttribute>>(
      `/api/proxy/tenants/${tenantId}/equipment/${equipmentId}/attributes`,
      { method: 'POST', body: { key, value, tenantMinRole } },
    )
    return r.entity
  }

  async updateAttribute(tenantId: string, equipmentId: string, attributeId: string, key: string, value: string, tenantMinRole: number): Promise<void> {
    await $fetch(`/api/proxy/tenants/${tenantId}/equipment/${equipmentId}/attributes/${attributeId}`, {
      method: 'PUT',
      body: { key, value, tenantMinRole },
    })
  }

  async deleteAttribute(tenantId: string, equipmentId: string, attributeId: string): Promise<void> {
    await $fetch(`/api/proxy/tenants/${tenantId}/equipment/${equipmentId}/attributes/${attributeId}`, { method: 'DELETE' })
  }
}
