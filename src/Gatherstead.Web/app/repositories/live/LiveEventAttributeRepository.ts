import type { IEventAttributeRepository } from '../interfaces'
import type { EventAttribute } from '../types'

interface ApiResponse<T> { entity: T; successful: boolean }

export class LiveEventAttributeRepository implements IEventAttributeRepository {
  async listAttributes(tenantId: string, eventId: string): Promise<EventAttribute[]> {
    const r = await $fetch<ApiResponse<EventAttribute[]>>(
      `/api/proxy/tenants/${tenantId}/events/${eventId}/attributes`,
    )
    return r.entity ?? []
  }

  async getAttribute(tenantId: string, eventId: string, attributeId: string): Promise<EventAttribute | null> {
    const r = await $fetch<ApiResponse<EventAttribute>>(
      `/api/proxy/tenants/${tenantId}/events/${eventId}/attributes/${attributeId}`,
    )
    return r.entity ?? null
  }

  async createAttribute(tenantId: string, eventId: string, key: string, value: string, tenantMinRole: number): Promise<EventAttribute> {
    const r = await $fetch<ApiResponse<EventAttribute>>(
      `/api/proxy/tenants/${tenantId}/events/${eventId}/attributes`,
      { method: 'POST', body: { key, value, tenantMinRole } },
    )
    return r.entity
  }

  async updateAttribute(tenantId: string, eventId: string, attributeId: string, key: string, value: string, tenantMinRole: number): Promise<void> {
    await $fetch(`/api/proxy/tenants/${tenantId}/events/${eventId}/attributes/${attributeId}`, {
      method: 'PUT',
      body: { key, value, tenantMinRole },
    })
  }

  async deleteAttribute(tenantId: string, eventId: string, attributeId: string): Promise<void> {
    await $fetch(`/api/proxy/tenants/${tenantId}/events/${eventId}/attributes/${attributeId}`, { method: 'DELETE' })
  }
}
