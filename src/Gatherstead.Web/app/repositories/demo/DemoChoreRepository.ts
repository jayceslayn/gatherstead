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

  async createTemplate(tenantId: string, eventId: string, name: string, timeSlots: number): Promise<ChoreTemplate> {
    const store = getDemoStore()
    if (store.choreTemplates.value.filter(t => t.eventId === eventId).length >= DEMO_LIMITS.choreTemplatesPerEvent) {
      throw new DemoLimitError('choreTemplatesPerEvent')
    }
    const t: ChoreTemplate = { id: demoId(), tenantId, eventId, name, timeSlots, minimumAssignees: null, notes: null }
    store.choreTemplates.value.push(t)
    persistDemoStore()
    return t
  }
}
