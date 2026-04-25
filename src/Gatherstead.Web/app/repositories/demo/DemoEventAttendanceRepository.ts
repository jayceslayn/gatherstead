import type { IEventAttendanceRepository } from '../interfaces'
import type { AttendanceRecord, AttendanceStatus } from '../types'
import { getDemoStore, persistDemoStore, demoId } from './DemoStore'

export class DemoEventAttendanceRepository implements IEventAttendanceRepository {
  async listAttendance(_tenantId: string, eventId: string): Promise<AttendanceRecord[]> {
    return getDemoStore().attendance.value.filter(a => a.eventId === eventId)
  }

  async upsertAttendance(
    _tenantId: string,
    eventId: string,
    _householdId: string,
    memberId: string,
    day: string,
    status: AttendanceStatus,
  ): Promise<void> {
    const store = getDemoStore()
    const idx = store.attendance.value.findIndex(
      a => a.eventId === eventId && a.householdMemberId === memberId && a.day === day,
    )
    if (idx >= 0) {
      store.attendance.value[idx] = { ...store.attendance.value[idx]!, status }
    }
    else {
      store.attendance.value.push({ id: demoId(), eventId, householdMemberId: memberId, day, status })
    }
    persistDemoStore()
  }

  async deleteAttendance(_tenantId: string, _eventId: string, attendanceId: string): Promise<void> {
    const store = getDemoStore()
    store.attendance.value = store.attendance.value.filter(a => a.id !== attendanceId)
    persistDemoStore()
  }
}
