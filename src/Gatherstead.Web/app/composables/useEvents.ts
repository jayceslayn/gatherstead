import { useTenantStore } from '~/stores/tenant'

export interface EventSummary {
  id: string
  tenantId: string
  propertyId: string
  name: string
  startDate: string
  endDate: string
}

interface EventsApiResponse {
  entity: EventSummary[]
  successful: boolean
}

interface EventApiResponse {
  entity: EventSummary
  successful: boolean
}

export function useEvents() {
  const tenantStore = useTenantStore()
  const config = useRuntimeConfig()

  if (config.public.demoMode) {
    return {
      events: ref<EventSummary[]>([]),
      upcomingEvents: ref<EventSummary[]>([]),
      pending: ref(false),
      error: ref(null),
      refresh: () => Promise.resolve(),
    }
  }

  const { data, pending, error, refresh } = useAsyncData<EventSummary[]>(
    () => `events-${tenantStore.currentTenantId}`,
    async () => {
      const response = await $fetch<EventsApiResponse>(
        `/api/proxy/tenants/${tenantStore.currentTenantId}/events`,
      )
      return response.entity ?? []
    },
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
  const config = useRuntimeConfig()

  if (config.public.demoMode) {
    return {
      event: ref<EventSummary | null>(null),
      pending: ref(false),
      error: ref(null),
    }
  }

  const { data, pending, error } = useAsyncData<EventSummary>(
    () => `event-${tenantStore.currentTenantId}-${eventId.value}`,
    async () => {
      const response = await $fetch<EventApiResponse>(
        `/api/proxy/tenants/${tenantStore.currentTenantId}/events/${eventId.value}`,
      )
      return response.entity
    },
    { watch: [eventId] },
  )

  const event = computed(() => data.value ?? null)
  return { event, pending, error }
}
