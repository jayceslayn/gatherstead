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
      toast.add({ title: translateError(e), color: 'error' })
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
      toast.add({ title: translateError(e), color: 'error' })
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

/**
 * Aggregates stay-request intents across every accommodation of a property and
 * exposes per-member lookups + bulk request/cancel actions — powering the event
 * sign-up day columns where each night lists accommodations and member requests.
 */
export function useEventAccommodationSignup(
  propertyId: Ref<string>,
  accommodations: Ref<AccommodationSummary[]>,
  householdMemberIds: Ref<string[]>,
) {
  const tenantStore = useTenantStore()
  const { accommodationIntents: repo } = useRepositories()
  const toast = useToast()
  const { translateError } = useApiError()

  const accommodationIds = computed(() => accommodations.value.map(a => a.id))

  // accommodationId → every intent (all households, all nights).
  const { data, pending, refresh } = useAsyncData<Record<string, AccommodationIntent[]>>(
    () => `accom-signup-${tenantStore.currentTenantId}-${propertyId.value}`,
    async () => {
      const tenantId = tenantStore.currentTenantId
      if (!tenantId || !propertyId.value || !accommodations.value.length) return {}
      const lists = await Promise.all(
        accommodations.value.map(a =>
          repo.listIntents(tenantId, propertyId.value, a.id).catch(() => [] as AccommodationIntent[]),
        ),
      )
      const map: Record<string, AccommodationIntent[]> = {}
      accommodations.value.forEach((a, i) => { map[a.id] = lists[i] ?? [] })
      return map
    },
    { watch: [propertyId, () => tenantStore.currentTenantId, () => accommodationIds.value.join(',')] },
  )

  const intentsByAccommodation = computed(() => data.value ?? {})

  // The selected household's stays whose span covers the given night, sorted by member order.
  function memberIntents(accommodationId: string, night: string): AccommodationIntent[] {
    const order = new Map(householdMemberIds.value.map((id, i) => [id, i]))
    return (intentsByAccommodation.value[accommodationId] ?? [])
      .filter(i => i.startNight <= night && i.endNight >= night && order.has(i.householdMemberId))
      .sort((a, b) => (order.get(a.householdMemberId) ?? 0) - (order.get(b.householdMemberId) ?? 0))
  }

  // Total occupancy for a night across all households — every stay whose span covers the night
  // (declined excluded; a party with no adult/child counts counts as 1). Capacity is only a soft
  // UI flag, so this can legitimately exceed it. Mirrors the event report's occupancy aggregation.
  function occupiedCount(accommodationId: string, night: string): number {
    return (intentsByAccommodation.value[accommodationId] ?? [])
      .filter(i => i.startNight <= night && i.endNight >= night && i.decision !== 'Declined')
      .reduce((sum, i) => sum + Math.max((i.partyAdults ?? 0) + (i.partyChildren ?? 0), 1), 0)
  }

  const updating = ref<string[]>([])

  /** Creates a single stay spanning [startNight, endNight]. */
  async function requestStay(
    accommodationId: string,
    householdId: string,
    memberId: string,
    startNight: string,
    endNight: string,
    status: AccommodationIntentStatus,
    notes: string | null,
    partyAdults: number | null,
    partyChildren: number | null,
  ): Promise<boolean> {
    const tenantId = tenantStore.currentTenantId
    if (!tenantId) return false
    updating.value = [...updating.value, accommodationId]
    try {
      await repo.createIntent(
        tenantId, propertyId.value, accommodationId, householdId, memberId,
        startNight, endNight, status, notes, partyAdults, partyChildren,
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

  /** Edits an existing stay as a unit (member + accommodation + range + party + status). */
  async function updateStay(
    accommodationId: string,
    intentId: string,
    memberId: string,
    targetAccommodationId: string,
    startNight: string,
    endNight: string,
    status: AccommodationIntentStatus,
    decision: AccommodationIntentDecision,
    notes: string | null,
    partyAdults: number | null,
    partyChildren: number | null,
  ): Promise<boolean> {
    const tenantId = tenantStore.currentTenantId
    if (!tenantId) return false
    updating.value = [...updating.value, intentId]
    try {
      // accommodationId is the stay's current location (path); targetAccommodationId is where it should land.
      await repo.updateIntent(
        tenantId, propertyId.value, accommodationId, intentId,
        memberId, targetAccommodationId,
        startNight, endNight, status, decision, notes, partyAdults, partyChildren,
      )
      await refresh()
      return true
    }
    catch (e) {
      toast.add({ title: translateError(e), color: 'error' })
      return false
    }
    finally {
      updating.value = updating.value.filter(k => k !== intentId)
    }
  }

  async function cancelStay(accommodationId: string, intentId: string) {
    const tenantId = tenantStore.currentTenantId
    if (!tenantId) return
    updating.value = [...updating.value, intentId]
    try {
      await repo.deleteIntent(tenantId, propertyId.value, accommodationId, intentId)
      await refresh()
    }
    catch (e) {
      toast.add({ title: translateError(e), color: 'error' })
    }
    finally {
      updating.value = updating.value.filter(k => k !== intentId)
    }
  }

  function isUpdating(key: string): boolean {
    return updating.value.includes(key)
  }

  return { pending, intentsByAccommodation, memberIntents, occupiedCount, requestStay, updateStay, cancelStay, isUpdating, refresh }
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
    decision: AccommodationIntentDecision,
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
        decision,
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
