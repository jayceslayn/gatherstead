import type { IMealPlanRepository } from '../interfaces'
import type { MealTemplate, MealPlan, MealIntent } from '../types'
import { mealTypesFromFlags } from '../types'
import { getDemoStore, persistDemoStore, demoId, DEMO_LIMITS, DemoLimitError } from './DemoStore'
import { enumDays } from './DemoHelpers'

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
    notes: string | null,
  ): Promise<MealTemplate> {
    const store = getDemoStore()
    if (store.mealTemplates.value.filter(t => t.eventId === eventId).length >= DEMO_LIMITS.mealTemplatesPerEvent) {
      throw new DemoLimitError('mealTemplatesPerEvent')
    }
    const t: MealTemplate = { id: demoId(), tenantId, eventId, name, mealTypes, notes }
    store.mealTemplates.value.push(t)

    const event = store.events.value.find(e => e.id === eventId)
    if (event) {
      const days = enumDays(event.startDate, event.endDate)
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

    persistDemoStore()
    return t
  }

  async updateTemplate(
    tenantId: string,
    eventId: string,
    templateId: string,
    name: string,
    mealTypes: number,
    notes: string | null,
  ): Promise<void> {
    const store = getDemoStore()
    const t = store.mealTemplates.value.find(x => x.id === templateId)
    if (!t) return

    if (t.mealTypes !== mealTypes) {
      const affectedPlanIds = store.mealPlans.value
        .filter(p => p.mealTemplateId === templateId)
        .map(p => p.id)
      store.mealAttendance.value = store.mealAttendance.value.filter(a => !affectedPlanIds.includes(a.mealPlanId))
      store.mealIntents.value = store.mealIntents.value.filter(i => !affectedPlanIds.includes(i.mealPlanId))
      store.mealPlans.value = store.mealPlans.value.filter(p => p.mealTemplateId !== templateId)

      const event = store.events.value.find(e => e.id === eventId)
      if (event) {
        const days = enumDays(event.startDate, event.endDate)
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
    t.notes = notes
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
