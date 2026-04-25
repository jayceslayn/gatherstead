import type { IMealAttendanceRepository } from '../interfaces'
import type { MealAttendance, AttendanceStatus } from '../types'
import { getDemoStore, persistDemoStore, demoId } from './DemoStore'

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
    return record
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
  }
}
