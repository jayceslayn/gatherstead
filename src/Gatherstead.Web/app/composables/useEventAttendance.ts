import { useTenantStore } from '~/stores/tenant'
import type { AttendanceStatus, AttendanceRecord } from '~/repositories/types'
import { DemoLimitError } from '~/repositories/interfaces'
import { useRepositories } from '~/composables/useRepositories'

export type { AttendanceStatus, AttendanceRecord }

export function useEventAttendance(eventId: Ref<string>) {
  const tenantStore = useTenantStore()
  const { eventAttendance: repo } = useRepositories()
  const { t } = useI18n()

  const { data, pending, error, refresh } = useAsyncData<AttendanceRecord[]>(
    () => `attendance-${tenantStore.currentTenantId}-${eventId.value}`,
    () => repo.listAttendance(tenantStore.currentTenantId!, eventId.value),
    { watch: [eventId] },
  )

  const attendance = computed(() => data.value ?? [])

  async function upsert(householdId: string, memberId: string, day: string, status: AttendanceStatus) {
    try {
      await repo.upsertAttendance(tenantStore.currentTenantId!, eventId.value, householdId, memberId, day, status)
      await refresh()
    }
    catch (e) {
      if (e instanceof DemoLimitError) {
        useToast().add({
          title: t('demo.limitReached.title'),
          description: t('demo.limitReached.description'),
          color: 'warning',
        })
        return
      }
      throw e
    }
  }

  async function deleteAttendance(attendanceId: string) {
    await repo.deleteAttendance(tenantStore.currentTenantId!, eventId.value, attendanceId)
    await refresh()
  }

  return { attendance, pending, error, refresh, upsert, deleteAttendance }
}
