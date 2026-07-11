import { useTenantStore } from '~/stores/tenant'
import type { AttendanceStatus, AttendanceRecord } from '~/repositories/types'
import { useRepositories } from '~/composables/useRepositories'
import { useTrackedAction } from '~/composables/useTrackedAction'


export function useEventAttendance(eventId: Ref<string>) {
  const tenantStore = useTenantStore()
  const { eventAttendance: repo } = useRepositories()

  const { data, pending, error, refresh } = useAsyncData<AttendanceRecord[]>(
    () => `attendance-${tenantStore.currentTenantId}-${eventId.value}`,
    () => repo.listAttendance(tenantStore.currentTenantId!, eventId.value),
    { watch: [eventId] },
  )

  const attendance = computed(() => data.value ?? [])

  const { run } = useTrackedAction(refresh)

  async function upsert(householdId: string, memberId: string, day: string, status: AttendanceStatus) {
    return run(memberId, () =>
      repo.upsertAttendance(tenantStore.currentTenantId!, eventId.value, householdId, memberId, day, status))
  }

  async function bulkUpsert(items: { memberId: string, day: string, status: AttendanceStatus }[]) {
    if (!items.length) return true
    return run('bulk', () =>
      repo.bulkUpsertAttendance(tenantStore.currentTenantId!, eventId.value, items))
  }

  async function deleteAttendance(attendanceId: string) {
    return run(attendanceId, () =>
      repo.deleteAttendance(tenantStore.currentTenantId!, eventId.value, attendanceId))
  }

  return { attendance, pending, error, refresh, upsert, bulkUpsert, deleteAttendance }
}
