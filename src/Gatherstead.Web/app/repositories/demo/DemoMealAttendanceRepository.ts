import type { BulkMealAttendanceItem, IMealAttendanceRepository } from '../interfaces'
import type { MealAttendance, AttendanceStatus } from '../types'
import { getDemoStore, persistDemoStore, demoId } from './DemoStore'
import { trackPersistence } from '../../utils/telemetry'

export class DemoMealAttendanceRepository implements IMealAttendanceRepository {
  async listMealAttendance(
    _tenantId: string,
    _eventId: string,
    _templateId: string,
    planId: string,
  ): Promise<MealAttendance[]> {
    return getDemoStore().mealAttendance.value.filter(a => a.mealPlanId === planId)
  }

  async upsertMealAttendance(
    tenantId: string,
    _eventId: string,
    _templateId: string,
    planId: string,
    _householdId: string,
    memberId: string,
    status: AttendanceStatus,
    bringOwnFood: boolean,
    notes?: string | null,
  ): Promise<MealAttendance> {
    const store = getDemoStore()
    const idx = store.mealAttendance.value.findIndex(
      a => a.mealPlanId === planId && a.householdMemberId === memberId,
    )
    if (idx >= 0) {
      const updated = { ...store.mealAttendance.value[idx]!, status, bringOwnFood, notes: notes ?? null }
      store.mealAttendance.value[idx] = updated
      persistDemoStore()
      trackPersistence('meal_attendance', 'set', { status })
      return updated
    }
    const record: MealAttendance = {
      id: demoId(),
      tenantId,
      mealPlanId: planId,
      householdMemberId: memberId,
      status,
      bringOwnFood,
      notes: notes ?? null,
    }
    store.mealAttendance.value.push(record)
    persistDemoStore()
    trackPersistence('meal_attendance', 'create', { status })
    return record
  }

  async listMealAttendanceForEvent(_tenantId: string, eventId: string): Promise<MealAttendance[]> {
    const store = getDemoStore()
    const templateIds = new Set(store.mealTemplates.value.filter(t => t.eventId === eventId).map(t => t.id))
    const planIds = new Set(store.mealPlans.value.filter(p => templateIds.has(p.mealTemplateId)).map(p => p.id))
    return store.mealAttendance.value.filter(a => planIds.has(a.mealPlanId))
  }

  async bulkUpsertMealAttendance(
    tenantId: string,
    eventId: string,
    items: BulkMealAttendanceItem[],
  ): Promise<MealAttendance[]> {
    const results: MealAttendance[] = []
    for (const item of items) {
      results.push(await this.upsertMealAttendance(tenantId, eventId, '', item.planId, '', item.memberId, item.status, false))
    }
    return results
  }

  async deleteMealAttendance(
    _tenantId: string,
    _eventId: string,
    _templateId: string,
    _planId: string,
    attendanceId: string,
  ): Promise<void> {
    const store = getDemoStore()
    store.mealAttendance.value = store.mealAttendance.value.filter(a => a.id !== attendanceId)
    persistDemoStore()
    trackPersistence('meal_attendance', 'delete')
  }
}
