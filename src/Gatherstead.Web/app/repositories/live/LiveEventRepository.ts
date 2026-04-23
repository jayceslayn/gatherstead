import type { IEventRepository } from '../interfaces'
import type { EventSummary } from '../types'

interface EventsApiResponse {
  entity: EventSummary[]
  successful: boolean
}

interface EventApiResponse {
  entity: EventSummary
  successful: boolean
}

export class LiveEventRepository implements IEventRepository {
  async listEvents(tenantId: string): Promise<EventSummary[]> {
    const r = await $fetch<EventsApiResponse>(`/api/proxy/tenants/${tenantId}/events`)
    return r.entity ?? []
  }

  async getEvent(tenantId: string, eventId: string): Promise<EventSummary | null> {
    const r = await $fetch<EventApiResponse>(`/api/proxy/tenants/${tenantId}/events/${eventId}`)
    return r.entity ?? null
  }
}
