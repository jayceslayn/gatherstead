import { useTenantStore } from '~/stores/tenant'
import type { EventSummary } from '~/repositories/types'
import { useRepositories } from '~/composables/useRepositories'

export type { EventSummary }

export function useEvents() {
  const tenantStore = useTenantStore()
  const { events: repo } = useRepositories()

  const { data, pending, error, refresh } = useAsyncData<EventSummary[]>(
    () => `events-${tenantStore.currentTenantId}`,
    () => repo.listEvents(tenantStore.currentTenantId!),
    { watch: [() => tenantStore.currentTenantId] },
  )

  const events = computed(() => data.value ?? [])
  const upcomingEvents = computed(() => {
    const today = new Date().toISOString().substring(0, 10)
    return [...events.value]
      .filter(e => e.endDate >= today)
      .sort((a, b) => a.startDate.localeCompare(b.startDate))
  })

  return { events, upcomingEvents, pending, error, refresh }
}

export function useEvent(eventId: Ref<string>) {
  const tenantStore = useTenantStore()
  const { events: repo } = useRepositories()

  const { data, pending, error } = useAsyncData<EventSummary | null>(
    () => `event-${tenantStore.currentTenantId}-${eventId.value}`,
    () => repo.getEvent(tenantStore.currentTenantId!, eventId.value),
    { watch: [eventId] },
  )

  return { event: computed(() => data.value ?? null), pending, error }
}
