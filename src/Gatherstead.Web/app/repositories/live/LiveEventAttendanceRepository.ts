import type { IEventAttendanceRepository } from '../interfaces'
import type { AttendanceRecord, AttendanceStatus } from '../types'

interface AttendanceApiResponse {
  entity: AttendanceRecord[]
  successful: boolean
}

export class LiveEventAttendanceRepository implements IEventAttendanceRepository {
  async listAttendance(tenantId: string, eventId: string): Promise<AttendanceRecord[]> {
    const r = await $fetch<AttendanceApiResponse>(
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
  }
}
