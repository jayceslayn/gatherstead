import { useTenantStore } from '~/stores/tenant'

export type AttendanceStatus = 'Going' | 'Maybe' | 'NotGoing'

export interface AttendanceRecord {
  id: string
  eventId: string
  householdMemberId: string
  day: string
  status: AttendanceStatus
}

interface AttendanceApiResponse {
  entity: AttendanceRecord[]
  successful: boolean
}

export function useEventAttendance(eventId: Ref<string>) {
  const tenantStore = useTenantStore()
  const config = useRuntimeConfig()

  if (config.public.demoMode) {
    return {
      attendance: ref<AttendanceRecord[]>([]),
      pending: ref(false),
      error: ref(null),
      refresh: () => Promise.resolve(),
      upsert: async (_householdId: string, _memberId: string, _day: string, _status: AttendanceStatus) => {},
    }
  }

  const { data, pending, error, refresh } = useAsyncData<AttendanceRecord[]>(
    () => `attendance-${tenantStore.currentTenantId}-${eventId.value}`,
    async () => {
      const response = await $fetch<AttendanceApiResponse>(
        `/api/proxy/tenants/${tenantStore.currentTenantId}/events/${eventId.value}/attendance`,
      )
      return response.entity ?? []
    },
    { watch: [eventId] },
  )

  const attendance = computed(() => data.value ?? [])

  async function upsert(householdId: string, memberId: string, day: string, status: AttendanceStatus) {
    await $fetch(
      `/api/proxy/tenants/${tenantStore.currentTenantId}/events/${eventId.value}/attendance?householdId=${encodeURIComponent(householdId)}`,
      {
        method: 'PUT',
        body: { householdMemberId: memberId, day, status },
      },
    )
    await refresh()
  }

  return { attendance, pending, error, refresh, upsert }
}
