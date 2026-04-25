import type { IChoreRepository } from '../interfaces'
import type { ChoreTemplate, ChorePlan, ChoreIntent } from '../types'
import { getDemoStore, persistDemoStore, demoId, DEMO_LIMITS, DemoLimitError } from './DemoStore'

export class DemoChoreRepository implements IChoreRepository {
  async listChoreTemplates(_tenantId: string, eventId: string): Promise<ChoreTemplate[]> {
    return getDemoStore().choreTemplates.value.filter(t => t.eventId === eventId)
  }

  async listPlans(_tenantId: string, _eventId: string, templateId: string): Promise<ChorePlan[]> {
    return getDemoStore().chorePlans.value.filter(p => p.templateId === templateId)
  }

  async listIntentsForMember(
    _tenantId: string,
    _eventId: string,
    _templateId: string,
    planId: string,
    memberId: string,
  ): Promise<ChoreIntent[]> {
    return getDemoStore().choreIntents.value.filter(
      i => i.chorePlanId === planId && i.householdMemberId === memberId,
    )
  }

  async upsertIntent(
    tenantId: string,
    _eventId: string,
    _templateId: string,
    planId: string,
    _householdId: string,
    memberId: string,
    volunteered: boolean,
  ): Promise<void> {
    const store = getDemoStore()
    const idx = store.choreIntents.value.findIndex(
      i => i.chorePlanId === planId && i.householdMemberId === memberId,
    )
    if (idx >= 0) {
      store.choreIntents.value[idx] = { ...store.choreIntents.value[idx]!, volunteered }
    }
    else {
      store.choreIntents.value.push({
        id: demoId(),
        tenantId,
        chorePlanId: planId,
        householdMemberId: memberId,
        volunteered,
      })
    }
    persistDemoStore()
  }

  async createTemplate(
    tenantId: string,
    eventId: string,
    name: string,
    timeSlots: number,
    minimumAssignees: number | null,
    notes: string | null,
  ): Promise<ChoreTemplate> {
    const store = getDemoStore()
    if (store.choreTemplates.value.filter(t => t.eventId === eventId).length >= DEMO_LIMITS.choreTemplatesPerEvent) {
      throw new DemoLimitError('choreTemplatesPerEvent')
    }
    const t: ChoreTemplate = { id: demoId(), tenantId, eventId, name, timeSlots, minimumAssignees, notes }
    store.choreTemplates.value.push(t)
    persistDemoStore()
    return t
  }

  async updateTemplate(
    _tenantId: string,
    _eventId: string,
    templateId: string,
    name: string,
    timeSlots: number,
    minimumAssignees: number | null,
    notes: string | null,
  ): Promise<void> {
    const store = getDemoStore()
    const t = store.choreTemplates.value.find(x => x.id === templateId)
    if (!t) return
    t.name = name
    t.timeSlots = timeSlots
    t.minimumAssignees = minimumAssignees
    t.notes = notes
    persistDemoStore()
  }

  async deleteTemplate(_tenantId: string, _eventId: string, templateId: string): Promise<void> {
    const store = getDemoStore()
    const planIds = store.chorePlans.value.filter(p => p.templateId === templateId).map(p => p.id)
    store.choreIntents.value = store.choreIntents.value.filter(i => !planIds.includes(i.chorePlanId))
    store.chorePlans.value = store.chorePlans.value.filter(p => p.templateId !== templateId)
    store.choreTemplates.value = store.choreTemplates.value.filter(t => t.id !== templateId)
    persistDemoStore()
  }

  async deleteIntent(
    _tenantId: string,
    _eventId: string,
    _templateId: string,
    _planId: string,
    intentId: string,
  ): Promise<void> {
    const store = getDemoStore()
    store.choreIntents.value = store.choreIntents.value.filter(i => i.id !== intentId)
    persistDemoStore()
  }
}
