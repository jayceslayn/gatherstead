import type { IMealPlanRepository } from '../interfaces'
import type { MealTemplate, MealPlan, MealIntent, AttributeWriteEntry, AttributeEntry } from '../types'
import { mealTypesFromFlags, taskSlotsFromFlags, mealTypeFlagsToTaskSlotFlags } from '../types'
import { getDemoStore, persistDemoStore, demoId, DEMO_LIMITS, DemoLimitError } from './DemoStore'
import { enumDays } from './DemoHelpers'

function toAttributeEntries(writes: AttributeWriteEntry[] | null | undefined): AttributeEntry[] {
  if (!writes) return []
  return writes.map(w => ({ id: demoId(), key: w.key, value: w.value, tenantMinRole: w.tenantMinRole, householdMinRole: w.householdMinRole ?? null }))
}

export class DemoMealPlanRepository implements IMealPlanRepository {
  async listMealTemplates(_tenantId: string, eventId: string): Promise<MealTemplate[]> {
    return getDemoStore().mealTemplates.value.filter(t => t.eventId === eventId)
  }

  async listPlans(_tenantId: string, _eventId: string, templateId: string): Promise<MealPlan[]> {
    return getDemoStore().mealPlans.value.filter(p => p.mealTemplateId === templateId)
  }

  async listIntentsForMember(
    _tenantId: string,
    _eventId: string,
    _templateId: string,
    planId: string,
    memberId: string,
  ): Promise<MealIntent[]> {
    return getDemoStore().mealIntents.value.filter(
      i => i.mealPlanId === planId && i.householdMemberId === memberId,
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
    const idx = store.mealIntents.value.findIndex(
      i => i.mealPlanId === planId && i.householdMemberId === memberId,
    )
    if (idx >= 0) {
      store.mealIntents.value[idx] = { ...store.mealIntents.value[idx]!, volunteered }
    }
    else {
      store.mealIntents.value.push({
        id: demoId(),
        tenantId,
        mealPlanId: planId,
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
    mealTypes: number,
    startDate: string | null,
    endDate: string | null,
    notes: string | null,
    attributes?: AttributeWriteEntry[] | null,
    createMatchingTaskTemplate?: boolean,
  ): Promise<MealTemplate> {
    const store = getDemoStore()
    if (store.mealTemplates.value.filter(t => t.eventId === eventId).length >= DEMO_LIMITS.mealTemplatesPerEvent) {
      throw new DemoLimitError('mealTemplatesPerEvent')
    }
    const t: MealTemplate = { id: demoId(), tenantId, eventId, name, mealTypes, startDate, endDate, notes, attributes: toAttributeEntries(attributes) }
    store.mealTemplates.value.push(t)

    const event = store.events.value.find(e => e.id === eventId)
    if (event) {
      const effectiveStart = startDate ?? event.startDate
      const effectiveEnd = endDate ?? event.endDate
      const days = enumDays(effectiveStart, effectiveEnd)
      for (const day of days) {
        for (const mealType of mealTypesFromFlags(mealTypes)) {
          store.mealPlans.value.push({
            id: demoId(),
            tenantId,
            mealTemplateId: t.id,
            day,
            mealType,
            notes: null,
            isException: false,
            exceptionReason: null,
          })
        }
      }
    }

    if (createMatchingTaskTemplate) {
      this.createMatchingTask(tenantId, eventId, t)
    }

    persistDemoStore()
    return t
  }

  // Mirrors the backend behavior: create a TaskTemplate alongside the meal so it can be
  // organized/assigned, mapping meal types to time slots (Breakfast→Morning, Lunch→Midday,
  // Dinner→Evening) via the shared helper.
  private createMatchingTask(tenantId: string, eventId: string, meal: MealTemplate): void {
    const store = getDemoStore()
    if (store.taskTemplates.value.filter(t => t.eventId === eventId).length >= DEMO_LIMITS.taskTemplatesPerEvent) {
      return
    }

    const existingNames = new Set(store.taskTemplates.value.filter(t => t.eventId === eventId).map(t => t.name))
    let candidate = meal.name
    if (existingNames.has(candidate)) candidate = `${meal.name} (cook)`
    if (existingNames.has(candidate)) return

    const timeSlots = mealTypeFlagsToTaskSlotFlags(meal.mealTypes)

    const taskTemplate = {
      id: demoId(),
      tenantId,
      eventId,
      name: candidate,
      timeSlots,
      minimumAssignees: null,
      notes: meal.notes,
      startDate: meal.startDate,
      endDate: meal.endDate,
      attributes: [] as AttributeEntry[],
    }
    store.taskTemplates.value.push(taskTemplate)

    const event = store.events.value.find(e => e.id === eventId)
    if (event) {
      const days = enumDays(meal.startDate ?? event.startDate, meal.endDate ?? event.endDate)
      for (const day of days) {
        for (const timeSlot of taskSlotsFromFlags(timeSlots)) {
          store.taskPlans.value.push({
            id: demoId(),
            tenantId,
            templateId: taskTemplate.id,
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

  async updateTemplate(
    tenantId: string,
    eventId: string,
    templateId: string,
    name: string,
    mealTypes: number,
    startDate: string | null,
    endDate: string | null,
    notes: string | null,
    attributes?: AttributeWriteEntry[] | null,
  ): Promise<void> {
    const store = getDemoStore()
    const t = store.mealTemplates.value.find(x => x.id === templateId)
    if (!t) return

    const plansNeedRegen = t.mealTypes !== mealTypes || t.startDate !== startDate || t.endDate !== endDate
    if (plansNeedRegen) {
      const affectedPlanIds = store.mealPlans.value
        .filter(p => p.mealTemplateId === templateId)
        .map(p => p.id)
      store.mealAttendance.value = store.mealAttendance.value.filter(a => !affectedPlanIds.includes(a.mealPlanId))
      store.mealIntents.value = store.mealIntents.value.filter(i => !affectedPlanIds.includes(i.mealPlanId))
      store.mealPlans.value = store.mealPlans.value.filter(p => p.mealTemplateId !== templateId)

      const event = store.events.value.find(e => e.id === eventId)
      if (event) {
        const effectiveStart = startDate ?? event.startDate
        const effectiveEnd = endDate ?? event.endDate
        const days = enumDays(effectiveStart, effectiveEnd)
        for (const day of days) {
          for (const mealType of mealTypesFromFlags(mealTypes)) {
            store.mealPlans.value.push({
              id: demoId(),
              tenantId,
              mealTemplateId: templateId,
              day,
              mealType,
              notes: null,
              isException: false,
              exceptionReason: null,
            })
          }
        }
      }
    }

    t.name = name
    t.mealTypes = mealTypes
    t.startDate = startDate
    t.endDate = endDate
    t.notes = notes
    if (attributes !== undefined) t.attributes = toAttributeEntries(attributes)
    persistDemoStore()
  }

  async deleteTemplate(_tenantId: string, _eventId: string, templateId: string): Promise<void> {
    const store = getDemoStore()
    const planIds = store.mealPlans.value.filter(p => p.mealTemplateId === templateId).map(p => p.id)
    store.mealAttendance.value = store.mealAttendance.value.filter(a => !planIds.includes(a.mealPlanId))
    store.mealIntents.value = store.mealIntents.value.filter(i => !planIds.includes(i.mealPlanId))
    store.mealPlans.value = store.mealPlans.value.filter(p => p.mealTemplateId !== templateId)
    store.mealTemplates.value = store.mealTemplates.value.filter(t => t.id !== templateId)
    persistDemoStore()
  }

  async updatePlan(
    _tenantId: string,
    _eventId: string,
    _templateId: string,
    planId: string,
    notes: string | null,
    isException: boolean,
    exceptionReason: string | null,
  ): Promise<void> {
    const store = getDemoStore()
    const idx = store.mealPlans.value.findIndex(p => p.id === planId)
    if (idx >= 0) {
      store.mealPlans.value[idx] = { ...store.mealPlans.value[idx]!, notes, isException, exceptionReason }
    }
    persistDemoStore()
  }

  async deletePlan(_tenantId: string, _eventId: string, _templateId: string, planId: string): Promise<void> {
    const store = getDemoStore()
    store.mealAttendance.value = store.mealAttendance.value.filter(a => a.mealPlanId !== planId)
    store.mealIntents.value = store.mealIntents.value.filter(i => i.mealPlanId !== planId)
    store.mealPlans.value = store.mealPlans.value.filter(p => p.id !== planId)
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
    store.mealIntents.value = store.mealIntents.value.filter(i => i.id !== intentId)
    persistDemoStore()
  }
}
