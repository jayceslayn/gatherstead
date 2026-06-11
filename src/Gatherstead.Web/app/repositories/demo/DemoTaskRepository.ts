import type { ITaskRepository } from '../interfaces'
import type { TaskTemplate, TaskPlan, TaskIntent, AttributeWriteEntry, AttributeEntry } from '../types'
import { taskSlotsFromFlags } from '../types'
import { getDemoStore, persistDemoStore, demoId, DEMO_LIMITS, DemoLimitError } from './DemoStore'
import { enumDays } from './DemoHelpers'

function toAttributeEntries(writes: AttributeWriteEntry[] | null | undefined): AttributeEntry[] {
  if (!writes) return []
  return writes.map(w => ({ id: demoId(), key: w.key, value: w.value, tenantMinRole: w.tenantMinRole, householdMinRole: w.householdMinRole ?? null }))
}

export class DemoTaskRepository implements ITaskRepository {
  async listTaskTemplates(_tenantId: string, eventId: string): Promise<TaskTemplate[]> {
    return getDemoStore().taskTemplates.value.filter(t => t.eventId === eventId)
  }

  async listPlans(_tenantId: string, _eventId: string, templateId: string): Promise<TaskPlan[]> {
    return getDemoStore().taskPlans.value.filter(p => p.templateId === templateId)
  }

  async listIntentsForMember(
    _tenantId: string,
    _eventId: string,
    _templateId: string,
    planId: string,
    memberId: string,
  ): Promise<TaskIntent[]> {
    return getDemoStore().taskIntents.value.filter(
      i => i.taskPlanId === planId && i.householdMemberId === memberId,
    )
  }

  async listPlanIntents(
    _tenantId: string,
    _eventId: string,
    _templateId: string,
    planId: string,
  ): Promise<TaskIntent[]> {
    return getDemoStore().taskIntents.value.filter(i => i.taskPlanId === planId)
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
    const idx = store.taskIntents.value.findIndex(
      i => i.taskPlanId === planId && i.householdMemberId === memberId,
    )
    if (idx >= 0) {
      store.taskIntents.value[idx] = { ...store.taskIntents.value[idx]!, volunteered }
    }
    else {
      store.taskIntents.value.push({
        id: demoId(),
        tenantId,
        taskPlanId: planId,
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
    startDate: string | null,
    endDate: string | null,
    minimumAssignees: number | null,
    notes: string | null,
    attributes?: AttributeWriteEntry[] | null,
  ): Promise<TaskTemplate> {
    const store = getDemoStore()
    if (store.taskTemplates.value.filter(t => t.eventId === eventId).length >= DEMO_LIMITS.taskTemplatesPerEvent) {
      throw new DemoLimitError('taskTemplatesPerEvent')
    }
    const t: TaskTemplate = { id: demoId(), tenantId, eventId, name, timeSlots, minimumAssignees, notes, startDate, endDate, attributes: toAttributeEntries(attributes) }
    store.taskTemplates.value.push(t)

    const event = store.events.value.find(e => e.id === eventId)
    if (event) {
      const effectiveStart = startDate ?? event.startDate
      const effectiveEnd = endDate ?? event.endDate
      const days = enumDays(effectiveStart, effectiveEnd)
      for (const day of days) {
        for (const timeSlot of taskSlotsFromFlags(timeSlots)) {
          store.taskPlans.value.push({
            id: demoId(),
            tenantId,
            templateId: t.id,
            day,
            timeSlot,
            completed: false,
            notes: null,
            isException: false,
            exceptionReason: null,
          })
        }
      }
    }

    persistDemoStore()
    return t
  }

  async updateTemplate(
    tenantId: string,
    eventId: string,
    templateId: string,
    name: string,
    timeSlots: number,
    startDate: string | null,
    endDate: string | null,
    minimumAssignees: number | null,
    notes: string | null,
    attributes?: AttributeWriteEntry[] | null,
  ): Promise<void> {
    const store = getDemoStore()
    const t = store.taskTemplates.value.find(x => x.id === templateId)
    if (!t) return

    const plansNeedRegen = t.timeSlots !== timeSlots || t.startDate !== startDate || t.endDate !== endDate
    if (plansNeedRegen) {
      const affectedPlanIds = store.taskPlans.value
        .filter(p => p.templateId === templateId)
        .map(p => p.id)
      store.taskIntents.value = store.taskIntents.value.filter(i => !affectedPlanIds.includes(i.taskPlanId))
      store.taskPlans.value = store.taskPlans.value.filter(p => p.templateId !== templateId)

      const event = store.events.value.find(e => e.id === eventId)
      if (event) {
        const effectiveStart = startDate ?? event.startDate
        const effectiveEnd = endDate ?? event.endDate
        const days = enumDays(effectiveStart, effectiveEnd)
        for (const day of days) {
          for (const timeSlot of taskSlotsFromFlags(timeSlots)) {
            store.taskPlans.value.push({
              id: demoId(),
              tenantId,
              templateId,
              day,
              timeSlot,
              completed: false,
              notes: null,
              isException: false,
              exceptionReason: null,
            })
          }
        }
      }
    }

    t.name = name
    t.timeSlots = timeSlots
    t.minimumAssignees = minimumAssignees
    t.notes = notes
    t.startDate = startDate
    t.endDate = endDate
    if (attributes !== undefined) t.attributes = toAttributeEntries(attributes)
    persistDemoStore()
  }

  async deleteTemplate(_tenantId: string, _eventId: string, templateId: string): Promise<void> {
    const store = getDemoStore()
    const planIds = store.taskPlans.value.filter(p => p.templateId === templateId).map(p => p.id)
    store.taskIntents.value = store.taskIntents.value.filter(i => !planIds.includes(i.taskPlanId))
    store.taskPlans.value = store.taskPlans.value.filter(p => p.templateId !== templateId)
    store.taskTemplates.value = store.taskTemplates.value.filter(t => t.id !== templateId)
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
    store.taskIntents.value = store.taskIntents.value.filter(i => i.id !== intentId)
    persistDemoStore()
  }
}
