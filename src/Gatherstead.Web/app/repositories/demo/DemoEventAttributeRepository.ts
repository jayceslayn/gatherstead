import type { IEventAttributeRepository } from '../interfaces'
import type { EventAttribute } from '../types'
import { getDemoStore, persistDemoStore, demoId } from './DemoStore'

export class DemoEventAttributeRepository implements IEventAttributeRepository {
  async listAttributes(tenantId: string, eventId: string): Promise<EventAttribute[]> {
    return getDemoStore().eventAttributes.value.filter(
      a => a.tenantId === tenantId && a.parentId === eventId,
    )
  }

  async getAttribute(tenantId: string, eventId: string, attributeId: string): Promise<EventAttribute | null> {
    return getDemoStore().eventAttributes.value.find(
      a => a.tenantId === tenantId && a.parentId === eventId && a.id === attributeId,
    ) ?? null
  }

  async createAttribute(tenantId: string, eventId: string, key: string, value: string, tenantMinRole: number): Promise<EventAttribute> {
    const store = getDemoStore()
    const a: EventAttribute = { id: demoId(), tenantId, parentId: eventId, key, value, tenantMinRole, householdMinRole: null }
    store.eventAttributes.value.push(a)
    persistDemoStore()
    return a
  }

  async updateAttribute(tenantId: string, eventId: string, attributeId: string, key: string, value: string, tenantMinRole: number): Promise<void> {
    const store = getDemoStore()
    const a = store.eventAttributes.value.find(
      x => x.tenantId === tenantId && x.parentId === eventId && x.id === attributeId,
    )
    if (!a) return
    a.key = key
    a.value = value
    a.tenantMinRole = tenantMinRole
    persistDemoStore()
  }

  async deleteAttribute(tenantId: string, eventId: string, attributeId: string): Promise<void> {
    const store = getDemoStore()
    store.eventAttributes.value = store.eventAttributes.value.filter(
      a => !(a.tenantId === tenantId && a.parentId === eventId && a.id === attributeId),
    )
    persistDemoStore()
  }
}
