import { useTenantStore } from '~/stores/tenant'
import type { EventReport } from '~/repositories/types'
import { useRepositories } from '~/composables/useRepositories'

export function useEventReport(eventId: Ref<string>) {
  const tenantStore = useTenantStore()
  const { reports: repo } = useRepositories()

  const { data, pending, error } = useAsyncData<EventReport | null>(
    () => `event-report-${tenantStore.currentTenantId}-${eventId.value}`,
    () => repo.getEventMealReport(tenantStore.currentTenantId!, eventId.value),
    { watch: [eventId] },
  )

  return { report: computed(() => data.value ?? null), pending, error }
}
