import { useTenantStore } from '~/stores/tenant'
import type {
  AccommodationSummary,
  AccommodationType,
  AccommodationIntent,
  AccommodationIntentStatus,
  AccommodationIntentDecision,
} from '~/repositories/types'
import { DemoLimitError } from '~/repositories/interfaces'
import { useRepositories } from '~/composables/useRepositories'

export type { AccommodationSummary, AccommodationType, AccommodationIntent, AccommodationIntentStatus, AccommodationIntentDecision }

export function useAccommodations(propertyId: Ref<string>) {
  const tenantStore = useTenantStore()
  const { accommodations: repo } = useRepositories()

  const { data, pending, error, refresh } = useAsyncData<AccommodationSummary[]>(
    () => `accommodations-${tenantStore.currentTenantId}-${propertyId.value}`,
    () => repo.listAccommodations(tenantStore.currentTenantId!, propertyId.value),
    { watch: [propertyId, () => tenantStore.currentTenantId] },
  )

  return { accommodations: computed(() => data.value ?? []), pending, error, refresh }
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
    capacityAdults: number | null,
    capacityChildren: number | null,
    notes: string | null,
  ) {
    updating.value.push('new')
    try {
      await repo.createAccommodation(
        tenantStore.currentTenantId!, propertyId.value, name, type, capacityAdults, capacityChildren, notes,
      )
      await refresh()
    }
    catch (e) {
      if (e instanceof DemoLimitError) {
        toast.add({ title: t('demo.limitReached.title'), description: t('demo.limitReached.description'), color: 'warning' })
        return
      }
      toast.add({ title: translateError(e as { code: string }), color: 'error' })
    }
    finally {
      updating.value = updating.value.filter(k => k !== 'new')
    }
  }

  async function updateAccommodation(
    accommodationId: string,
    name: string,
    type: AccommodationType,
    capacityAdults: number | null,
    capacityChildren: number | null,
    notes: string | null,
  ) {
    updating.value.push(accommodationId)
    try {
      await repo.updateAccommodation(
        tenantStore.currentTenantId!, propertyId.value, accommodationId, name, type, capacityAdults, capacityChildren, notes,
      )
      await refresh()
    }
    catch (e) {
      toast.add({ title: translateError(e as { code: string }), color: 'error' })
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
      toast.add({ title: translateError(e as { code: string }), color: 'error' })
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
    night: string,
    status: AccommodationIntentStatus,
    notes?: string | null,
    partySize?: number | null,
  ) {
    const key = `${night}-${memberId}`
    updating.value.push(key)
    try {
      await repo.createIntent(
        tenantStore.currentTenantId!,
        propertyId.value,
        accommodationId.value,
        householdId,
        memberId,
        night,
        status,
        notes,
        partySize,
      )
      await refresh()
    }
    catch (e) {
      toast.add({ title: translateError(e as { code: string }), color: 'error' })
    }
    finally {
      updating.value = updating.value.filter(k => k !== key)
    }
  }

  async function promoteIntent(
    intentId: string,
    status: AccommodationIntentStatus,
    decision: AccommodationIntentDecision,
    notes?: string | null,
    partySize?: number | null,
  ) {
    updating.value.push(intentId)
    try {
      await repo.updateIntent(
        tenantStore.currentTenantId!,
        propertyId.value,
        accommodationId.value,
        intentId,
        status,
        decision,
        notes,
        partySize,
      )
      await refresh()
    }
    catch (e) {
      toast.add({ title: translateError(e as { code: string }), color: 'error' })
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
      toast.add({ title: translateError(e as { code: string }), color: 'error' })
    }
    finally {
      updating.value = updating.value.filter(k => k !== intentId)
    }
  }

  return { updating, requestIntent, promoteIntent, deleteIntent }
}
