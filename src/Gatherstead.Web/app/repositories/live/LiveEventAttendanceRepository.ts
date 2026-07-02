import type { BulkEventAttendanceItem, IEventAttendanceRepository } from '../interfaces'
import type { AttendanceRecord, AttendanceStatus } from '../types'
import { trackPersistence } from '../../utils/telemetry'
import { retryOn429 } from './retryOn429'

interface ApiResponse<T> { entity: T; successful: boolean }

export class LiveEventAttendanceRepository implements IEventAttendanceRepository {
  async listAttendance(tenantId: string, eventId: string): Promise<AttendanceRecord[]> {
    const r = await $fetch<ApiResponse<AttendanceRecord[]>>(
      `/api/proxy/tenants/${tenantId}/events/${eventId}/attendance`,
    )
    return r.entity ?? []
  }

  async upsertAttendance(
    tenantId: string,
    eventId: string,
    householdId: string,
    memberId: string,
    day: string,
    status: AttendanceStatus,
  ): Promise<void> {
    await $fetch(
      `/api/proxy/tenants/${tenantId}/events/${eventId}/attendance?householdId=${encodeURIComponent(householdId)}`,
      {
        method: 'PUT',
        body: { householdMemberId: memberId, day, status },
      },
    )
    trackPersistence('attendance', 'set', { status })
  }

  async bulkUpsertAttendance(
    tenantId: string,
    eventId: string,
    items: BulkEventAttendanceItem[],
  ): Promise<void> {
    if (!items.length) return
    await retryOn429(() => $fetch<unknown>(
      `/api/proxy/tenants/${tenantId}/events/${eventId}/attendance/bulk`,
      {
        method: 'PUT',
        body: {
          items: items.map(i => ({ householdMemberId: i.memberId, day: i.day, status: i.status })),
        },
      },
    ))
    trackPersistence('attendance', 'set', { count: items.length })
  }

  async deleteAttendance(tenantId: string, eventId: string, attendanceId: string): Promise<void> {
    await $fetch(
      `/api/proxy/tenants/${tenantId}/events/${eventId}/attendance/${attendanceId}`,
      { method: 'DELETE' },
    )
    trackPersistence('attendance', 'delete')
  }
}
