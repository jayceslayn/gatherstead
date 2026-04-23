import type { IEventRepository } from '../interfaces'
import type { EventSummary } from '../types'
import { getDemoStore, persistDemoStore, demoId, DEMO_LIMITS, DemoLimitError } from './DemoStore'

export class DemoEventRepository implements IEventRepository {
  async listEvents(tenantId: string): Promise<EventSummary[]> {
    return getDemoStore().events.value.filter(e => e.tenantId === tenantId)
  }

  async getEvent(tenantId: string, eventId: string): Promise<EventSummary | null> {
    return getDemoStore().events.value.find(
      e => e.tenantId === tenantId && e.id === eventId,
    ) ?? null
  }

  async createEvent(
    tenantId: string,
    propertyId: string,
    name: string,
    startDate: string,
    endDate: string,
  ): Promise<EventSummary> {
    const store = getDemoStore()
    if (store.events.value.filter(e => e.tenantId === tenantId).length >= DEMO_LIMITS.events) {
      throw new DemoLimitError('events')
    }
    const start = new Date(startDate)
    const end = new Date(endDate)
    const days = Math.round((end.getTime() - start.getTime()) / 86400000) + 1
    if (days > DEMO_LIMITS.eventMaxDays) {
      throw new DemoLimitError('eventMaxDays')
    }
    const e: EventSummary = { id: demoId(), tenantId, propertyId, name, startDate, endDate }
    store.events.value.push(e)
    persistDemoStore()
    return e
  }
}
