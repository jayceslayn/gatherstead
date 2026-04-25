import type { IEventRepository } from '../interfaces'
import type { EventSummary } from '../types'

interface ApiResponse<T> { entity: T; successful: boolean }

export class LiveEventRepository implements IEventRepository {
  async listEvents(tenantId: string): Promise<EventSummary[]> {
    const r = await $fetch<ApiResponse<EventSummary[]>>(`/api/proxy/tenants/${tenantId}/events`)
    return r.entity ?? []
  }

  async getEvent(tenantId: string, eventId: string): Promise<EventSummary | null> {
    const r = await $fetch<ApiResponse<EventSummary>>(`/api/proxy/tenants/${tenantId}/events/${eventId}`)
    return r.entity ?? null
  }

  async createEvent(
    tenantId: string,
    propertyId: string,
    name: string,
    startDate: string,
    endDate: string,
  ): Promise<EventSummary> {
    const r = await $fetch<ApiResponse<EventSummary>>(
      `/api/proxy/tenants/${tenantId}/events`,
      { method: 'POST', body: { propertyId, name, startDate, endDate } },
    )
    return r.entity
  }

  async updateEvent(
    tenantId: string,
    eventId: string,
    name: string,
    startDate: string,
    endDate: string,
  ): Promise<void> {
    await $fetch(`/api/proxy/tenants/${tenantId}/events/${eventId}`, {
      method: 'PUT',
      body: { name, startDate, endDate },
    })
  }

  async deleteEvent(tenantId: string, eventId: string): Promise<void> {
    await $fetch(`/api/proxy/tenants/${tenantId}/events/${eventId}`, { method: 'DELETE' })
  }
}
