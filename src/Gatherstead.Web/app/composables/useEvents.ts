import { useTenantStore } from '~/stores/tenant'
import type { EventSummary } from '~/repositories/types'
import { DemoLimitError } from '~/repositories/interfaces'
import { useRepositories } from '~/composables/useRepositories'


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

  const { data, pending, error, refresh } = useAsyncData<EventSummary | null>(
    () => `event-${tenantStore.currentTenantId}-${eventId.value}`,
    () => repo.getEvent(tenantStore.currentTenantId!, eventId.value),
    { watch: [eventId] },
  )

  return { event: computed(() => data.value ?? null), pending, error, refresh }
}

export function useEventActions(refresh: () => Promise<void>) {
  const tenantStore = useTenantStore()
  const { events: repo } = useRepositories()
  const toast = useToast()
  const { t } = useI18n()
  const { translateError } = useApiError()
  const updating = ref<string[]>([])

  async function createEvent(propertyId: string, name: string, startDate: string, endDate: string): Promise<EventSummary | null> {
    updating.value.push('new')
    try {
      const created = await repo.createEvent(tenantStore.currentTenantId!, propertyId, name, startDate, endDate)
      await refresh()
      return created
    }
    catch (e) {
      if (e instanceof DemoLimitError) {
        toast.add({ title: t('demo.limitReached.title'), description: t('demo.limitReached.description'), color: 'warning' })
        return null
      }
      toast.add({ title: translateError(e), color: 'error' })
      return null
    }
    finally {
      updating.value = updating.value.filter(k => k !== 'new')
    }
  }

  async function updateEvent(eventId: string, name: string, startDate: string, endDate: string): Promise<boolean> {
    updating.value.push(eventId)
    try {
      await repo.updateEvent(tenantStore.currentTenantId!, eventId, name, startDate, endDate)
      await refresh()
      return true
    }
    catch (e) {
      toast.add({ title: translateError(e), color: 'error' })
      return false
    }
    finally {
      updating.value = updating.value.filter(k => k !== eventId)
    }
  }

  async function deleteEvent(eventId: string) {
    updating.value.push(eventId)
    try {
      await repo.deleteEvent(tenantStore.currentTenantId!, eventId)
      await refresh()
    }
    catch (e) {
      toast.add({ title: translateError(e), color: 'error' })
    }
    finally {
      updating.value = updating.value.filter(k => k !== eventId)
    }
  }

  return { updating, createEvent, updateEvent, deleteEvent }
}
