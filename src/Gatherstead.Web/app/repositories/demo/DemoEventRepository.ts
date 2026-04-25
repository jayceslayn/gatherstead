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

  async updateEvent(
    _tenantId: string,
    eventId: string,
    name: string,
    startDate: string,
    endDate: string,
  ): Promise<void> {
    const store = getDemoStore()
    const e = store.events.value.find(x => x.id === eventId)
    if (!e) return
    e.name = name
    e.startDate = startDate
    e.endDate = endDate
    persistDemoStore()
  }

  async deleteEvent(_tenantId: string, eventId: string): Promise<void> {
    const store = getDemoStore()
    const planIds = [
      ...store.mealPlans.value.filter(p => {
        const t = store.mealTemplates.value.find(t => t.id === p.mealTemplateId)
        return t?.eventId === eventId
      }).map(p => p.id),
      ...store.chorePlans.value.filter(p => {
        const t = store.choreTemplates.value.find(t => t.id === p.templateId)
        return t?.eventId === eventId
      }).map(p => p.id),
    ]
    store.mealIntents.value = store.mealIntents.value.filter(i => !planIds.includes(i.mealPlanId))
    store.choreIntents.value = store.choreIntents.value.filter(i => !planIds.includes(i.chorePlanId))
    store.mealPlans.value = store.mealPlans.value.filter(p => !planIds.includes(p.id))
    store.chorePlans.value = store.chorePlans.value.filter(p => !planIds.includes(p.id))
    store.mealTemplates.value = store.mealTemplates.value.filter(t => t.eventId !== eventId)
    store.choreTemplates.value = store.choreTemplates.value.filter(t => t.eventId !== eventId)
    store.attendance.value = store.attendance.value.filter(a => a.eventId !== eventId)
    store.events.value = store.events.value.filter(e => e.id !== eventId)
    persistDemoStore()
  }
}
