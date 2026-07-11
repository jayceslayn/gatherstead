import { useTenantStore } from '~/stores/tenant'
import type {
  AccommodationSummary,
  AccommodationType,
  AccommodationIntent,
  AccommodationIntentStatus,
  BedWriteEntry,
  AccommodationAvailability,
} from '~/repositories/types'
import { DemoLimitError } from '~/repositories/interfaces'
import type { AccommodationAvailabilityQuery, AccommodationDimensions } from '~/repositories/interfaces'
import { useRepositories } from '~/composables/useRepositories'
import { useEntityList } from '~/composables/useEntityList'
import { useTrackedAction } from '~/composables/useTrackedAction'
import { compareAccommodations, compareAvailability } from '~/utils/sorting'


export function useAccommodations(propertyId: Ref<string>) {
  const tenantStore = useTenantStore()
  const { accommodations: repo } = useRepositories()

  const { items: accommodations, pending, error, refresh } = useEntityList<AccommodationSummary>(
    () => `accommodations-${tenantStore.currentTenantId}-${propertyId.value}`,
    () => repo.listAccommodations(tenantStore.currentTenantId!, propertyId.value),
    { watch: [propertyId, () => tenantStore.currentTenantId], sort: compareAccommodations },
  )

  return { accommodations, pending, error, refresh }
}

/**
 * Drives the top-level "Expedia-like" availability search. Call `search(query)` with party size +
 * nights; `results` then holds every accommodation with its remaining capacity for that window.
 */
export function useAccommodationSearch() {
  const tenantStore = useTenantStore()
  const { accommodations: repo } = useRepositories()

  const params = ref<AccommodationAvailabilityQuery | null>(null)

  const { data, pending, error, refresh } = useAsyncData<AccommodationAvailability[]>(
    () => `accom-availability-${tenantStore.currentTenantId}-${params.value ? JSON.stringify(params.value) : 'none'}`,
    () => {
      const tenantId = tenantStore.currentTenantId
      if (!tenantId || !params.value) return Promise.resolve([])
      return repo.searchAvailability(tenantId, params.value)
    },
    { watch: [() => tenantStore.currentTenantId, params] },
  )

  function search(query: AccommodationAvailabilityQuery) {
    params.value = { ...query }
  }

  return {
    results: computed(() => [...(data.value ?? [])].sort(compareAvailability)),
    hasSearched: computed(() => params.value !== null),
    params: computed(() => params.value),
    pending,
    error,
    refresh,
    search,
  }
}

/** Creates a stay request for any accommodation (property resolved from the search result). */
export function useAccommodationStayRequest() {
  const tenantStore = useTenantStore()
  const { accommodationIntents: repo } = useRepositories()
  const { updating, run } = useTrackedAction()
  const submitting = computed(() => updating.value.length > 0)

  async function requestStay(
    propertyId: string,
    accommodationId: string,
    householdId: string,
    memberId: string,
    startNight: string,
    endNight: string,
    status: AccommodationIntentStatus,
    partyAdults: number | null,
    partyChildren: number | null,
    notes: string | null,
  ): Promise<boolean> {
    const tenantId = tenantStore.currentTenantId
    if (!tenantId) return false
    return run('new', () => repo.createIntent(
      tenantId, propertyId, accommodationId, householdId, memberId,
      startNight, endNight, status, notes, partyAdults, partyChildren,
    ))
  }

  return { submitting, requestStay }
}

export function useAccommodationIntents(
  propertyId: Ref<string>,
  accommodationId: Ref<string>,
  memberId?: Ref<string | null>,
) {
  const tenantStore = useTenantStore()
  const { accommodationIntents: repo } = useRepositories()

  const { data, pending, error, refresh } = useAsyncData<AccommodationIntent[]>(
    () => `accom-intents-${tenantStore.currentTenantId}-${accommodationId.value}-${memberId?.value ?? 'all'}`,
    async () => {
      if (memberId?.value) {
        return repo.listIntentsForMember(
          tenantStore.currentTenantId!,
          propertyId.value,
          accommodationId.value,
          memberId.value,
        )
      }
      return repo.listIntents(tenantStore.currentTenantId!, propertyId.value, accommodationId.value)
    },
    {
      watch: [
        accommodationId,
        () => tenantStore.currentTenantId,
        ...(memberId ? [memberId] : []),
      ],
    },
  )

  return { intents: computed(() => data.value ?? []), pending, error, refresh }
}

export function useAccommodationActions(propertyId: Ref<string>, refresh: () => Promise<void>) {
  const tenantStore = useTenantStore()
  const { accommodations: repo } = useRepositories()
  const toast = useToast()
  const { t } = useI18n()
  const { translateError } = useApiError()
  const updating = ref<string[]>([])

  async function createAccommodation(
    name: string,
    type: AccommodationType,
    dimensions: AccommodationDimensions,
    beds: BedWriteEntry[],
    notes: string | null,
  ): Promise<boolean> {
    updating.value.push('new')
    try {
      await repo.createAccommodation(
        tenantStore.currentTenantId!, propertyId.value, name, type, dimensions, beds, notes,
      )
      await refresh()
      return true
    }
    catch (e) {
      if (e instanceof DemoLimitError) {
        toast.add({ title: t('demo.limitReached.title'), description: t('demo.limitReached.description'), color: 'warning' })
        return false
      }
      toast.add({ title: translateError(e), color: 'error' })
      return false
    }
    finally {
      updating.value = updating.value.filter(k => k !== 'new')
    }
  }

  async function updateAccommodation(
    accommodationId: string,
    name: string,
    type: AccommodationType,
    dimensions: AccommodationDimensions,
    beds: BedWriteEntry[],
    notes: string | null,
  ): Promise<boolean> {
    updating.value.push(accommodationId)
    try {
      await repo.updateAccommodation(
        tenantStore.currentTenantId!, propertyId.value, accommodationId, name, type, dimensions, beds, notes,
      )
      await refresh()
      return true
    }
    catch (e) {
      toast.add({ title: translateError(e), color: 'error' })
      return false
    }
    finally {
      updating.value = updating.value.filter(k => k !== accommodationId)
    }
  }

  async function deleteAccommodation(accommodationId: string) {
    updating.value.push(accommodationId)
    try {
      await repo.deleteAccommodation(tenantStore.currentTenantId!, propertyId.value, accommodationId)
      await refresh()
    }
    catch (e) {
      toast.add({ title: translateError(e), color: 'error' })
    }
    finally {
      updating.value = updating.value.filter(k => k !== accommodationId)
    }
  }

  return { updating, createAccommodation, updateAccommodation, deleteAccommodation }
}

export function useAccommodationIntentActions(
  propertyId: Ref<string>,
  accommodationId: Ref<string>,
  refresh: () => Promise<void>,
) {
  const tenantStore = useTenantStore()
  const { accommodationIntents: repo } = useRepositories()
  const toast = useToast()
  const { translateError } = useApiError()
  const updating = ref<string[]>([])

  async function requestIntent(
    householdId: string,
    memberId: string,
    startNight: string,
    endNight: string,
    status: AccommodationIntentStatus,
    notes?: string | null,
    partyAdults?: number | null,
    partyChildren?: number | null,
  ) {
    const key = `${startNight}-${memberId}`
    updating.value.push(key)
    try {
      await repo.createIntent(
        tenantStore.currentTenantId!,
        propertyId.value,
        accommodationId.value,
        householdId,
        memberId,
        startNight,
        endNight,
        status,
        notes,
        partyAdults,
        partyChildren,
      )
      await refresh()
    }
    catch (e) {
      toast.add({ title: translateError(e), color: 'error' })
    }
    finally {
      updating.value = updating.value.filter(k => k !== key)
    }
  }

  async function promoteIntent(
    intentId: string,
    memberId: string,
    startNight: string,
    endNight: string,
    status: AccommodationIntentStatus,
    notes?: string | null,
    partyAdults?: number | null,
    partyChildren?: number | null,
  ) {
    updating.value.push(intentId)
    try {
      // Promotion never moves the stay, so the target accommodation is the current one.
      await repo.updateIntent(
        tenantStore.currentTenantId!,
        propertyId.value,
        accommodationId.value,
        intentId,
        memberId,
        accommodationId.value,
        startNight,
        endNight,
        status,
        notes,
        partyAdults,
        partyChildren,
      )
      await refresh()
    }
    catch (e) {
      toast.add({ title: translateError(e), color: 'error' })
    }
    finally {
      updating.value = updating.value.filter(k => k !== intentId)
    }
  }

  async function deleteIntent(intentId: string) {
    updating.value.push(intentId)
    try {
      await repo.deleteIntent(
        tenantStore.currentTenantId!,
        propertyId.value,
        accommodationId.value,
        intentId,
      )
      await refresh()
    }
    catch (e) {
      toast.add({ title: translateError(e), color: 'error' })
    }
    finally {
      updating.value = updating.value.filter(k => k !== intentId)
    }
  }

  return { updating, requestIntent, promoteIntent, deleteIntent }
}
