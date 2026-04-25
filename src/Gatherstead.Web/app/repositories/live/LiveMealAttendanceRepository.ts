import type { IMealAttendanceRepository } from '../interfaces'
import type { MealAttendance, AttendanceStatus } from '../types'

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
  }
}
