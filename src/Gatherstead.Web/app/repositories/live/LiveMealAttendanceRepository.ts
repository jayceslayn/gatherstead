import type { BulkMealAttendanceItem, IMealAttendanceRepository } from '../interfaces'
import type { MealAttendance, AttendanceStatus } from '../types'
import { trackPersistence } from '../../utils/telemetry'
import { retryOn429 } from './retryOn429'

interface ApiResponse<T> { entity: T; successful: boolean }

export class LiveMealAttendanceRepository implements IMealAttendanceRepository {
  async listMealAttendance(
    tenantId: string,
    eventId: string,
    templateId: string,
    planId: string,
  ): Promise<MealAttendance[]> {
    const r = await $fetch<ApiResponse<MealAttendance[]>>(
      `/api/proxy/tenants/${tenantId}/events/${eventId}/meal-templates/${templateId}/plans/${planId}/attendance`,
    )
    return r.entity ?? []
  }

  async listMealAttendanceForEvent(tenantId: string, eventId: string): Promise<MealAttendance[]> {
    const r = await $fetch<ApiResponse<MealAttendance[]>>(
      `/api/proxy/tenants/${tenantId}/events/${eventId}/meal-attendance`,
    )
    return r.entity ?? []
  }

  async bulkUpsertMealAttendance(
    tenantId: string,
    eventId: string,
    items: BulkMealAttendanceItem[],
  ): Promise<MealAttendance[]> {
    if (!items.length) return []
    const r = await retryOn429(() => $fetch<ApiResponse<MealAttendance[]>>(
      `/api/proxy/tenants/${tenantId}/events/${eventId}/meal-attendance/bulk`,
      {
        method: 'PUT',
        body: {
          items: items.map(i => ({
            mealPlanId: i.planId,
            householdMemberId: i.memberId,
            status: i.status,
            bringOwnFood: false,
          })),
        },
      },
    ))
    trackPersistence('meal_attendance', 'set', { count: items.length })
    return r.entity ?? []
  }

  async upsertMealAttendance(
    tenantId: string,
    eventId: string,
    templateId: string,
    planId: string,
    householdId: string,
    memberId: string,
    status: AttendanceStatus,
    bringOwnFood: boolean,
    notes?: string | null,
  ): Promise<MealAttendance> {
    const r = await $fetch<ApiResponse<MealAttendance>>(
      `/api/proxy/tenants/${tenantId}/events/${eventId}/meal-templates/${templateId}/plans/${planId}/attendance?householdId=${encodeURIComponent(householdId)}`,
      {
        method: 'PUT',
        body: { householdMemberId: memberId, status, bringOwnFood, notes },
      },
    )
    trackPersistence('meal_attendance', 'set', { status })
    return r.entity
  }

  async deleteMealAttendance(
    tenantId: string,
    eventId: string,
    templateId: string,
    planId: string,
    attendanceId: string,
  ): Promise<void> {
    await $fetch(
      `/api/proxy/tenants/${tenantId}/events/${eventId}/meal-templates/${templateId}/plans/${planId}/attendance/${attendanceId}`,
      { method: 'DELETE' },
    )
    trackPersistence('meal_attendance', 'delete')
  }
}
