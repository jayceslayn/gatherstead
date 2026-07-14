import { useTenantStore } from '~/stores/tenant'
import { useCurrentMemberStore } from '~/stores/member'
import { useRepositories } from '~/composables/useRepositories'
import type {
  AccommodationIntent,
  AccommodationIntentStatus,
  MyMeal,
  MyStay,
  MyTask,
  ShoppingItem,
} from '~/repositories/types'
import { today } from '~/utils/dates'

/** The current member's stays that haven't ended yet (dashboard + accommodations page widget). */
export function useMyStays() {
  const tenantStore = useTenantStore()
  const memberStore = useCurrentMemberStore()
  const { accommodations: repo } = useRepositories()

  const { data, pending, error, refresh } = useAsyncData<MyStay[]>(
    () => `my-stays-${tenantStore.currentTenantId}-${memberStore.linkedMemberId ?? 'none'}`,
    () => {
      const tenantId = tenantStore.currentTenantId
      const memberId = memberStore.linkedMemberId
      if (!tenantId || !memberId) return Promise.resolve([])
      return repo.listMyStays(tenantId, memberId, today())
    },
    { watch: [() => tenantStore.currentTenantId, () => memberStore.linkedMemberId] },
  )

  return { stays: computed(() => data.value ?? []), pending, error, refresh }
}

/**
 * View/edit/delete actions for a member's own stay. A `MyStay` carries its own property + accommodation
 * ids, so each action takes the stay (rather than binding a single accommodation via refs like
 * `useAccommodationIntentActions`), letting one widget span stays across many accommodations.
 */
export function useMyStayActions() {
  const tenantStore = useTenantStore()
  const { accommodationIntents: repo } = useRepositories()
  const toast = useToast()
  const { translateError } = useApiError()
  const submitting = ref(false)
  const deleting = ref(false)

  // `MyStay` omits `notes`, so load the full intent before editing to avoid blanking it on save.
  async function loadIntent(stay: MyStay): Promise<AccommodationIntent | null> {
    const tenantId = tenantStore.currentTenantId
    if (!tenantId) return null
    try {
      const intents = await repo.listIntentsForMember(
        tenantId, stay.propertyId, stay.accommodationId, stay.householdMemberId,
      )
      return intents.find(i => i.id === stay.id) ?? null
    }
    catch (e) {
      toast.add({ title: translateError(e), color: 'error' })
      return null
    }
  }

  async function updateStay(
    stay: MyStay,
    payload: {
      accommodationId: string
      memberId: string
      startNight: string
      endNight: string
      status: AccommodationIntentStatus
      notes: string | null
      partyAdults: number | null
      partyChildren: number | null
    },
  ): Promise<boolean> {
    const tenantId = tenantStore.currentTenantId
    if (!tenantId) return false
    submitting.value = true
    try {
      await repo.updateIntent(
        tenantId, stay.propertyId, stay.accommodationId, stay.id,
        payload.memberId, payload.accommodationId,
        payload.startNight, payload.endNight, payload.status,
        payload.notes, payload.partyAdults, payload.partyChildren,
      )
      return true
    }
    catch (e) {
      toast.add({ title: translateError(e), color: 'error' })
      return false
    }
    finally {
      submitting.value = false
    }
  }

  async function deleteStay(stay: MyStay): Promise<boolean> {
    const tenantId = tenantStore.currentTenantId
    if (!tenantId) return false
    deleting.value = true
    try {
      await repo.deleteIntent(tenantId, stay.propertyId, stay.accommodationId, stay.id)
      return true
    }
    catch (e) {
      toast.add({ title: translateError(e), color: 'error' })
      return false
    }
    finally {
      deleting.value = false
    }
  }

  return { submitting, deleting, loadIntent, updateStay, deleteStay }
}

/** The current member's volunteered tasks scheduled today or later. */
export function useMyTasks() {
  const tenantStore = useTenantStore()
  const memberStore = useCurrentMemberStore()
  const { tasks: repo } = useRepositories()

  const { data, pending, error, refresh } = useAsyncData<MyTask[]>(
    () => `my-tasks-${tenantStore.currentTenantId}-${memberStore.linkedMemberId ?? 'none'}`,
    () => {
      const tenantId = tenantStore.currentTenantId
      const memberId = memberStore.linkedMemberId
      if (!tenantId || !memberId) return Promise.resolve([])
      return repo.listMyTasks(tenantId, memberId, today())
    },
    { watch: [() => tenantStore.currentTenantId, () => memberStore.linkedMemberId] },
  )

  return { tasks: computed(() => data.value ?? []), pending, error, refresh }
}

/** The current member's volunteered cook sign-ups scheduled today or later. */
export function useMyMeals() {
  const tenantStore = useTenantStore()
  const memberStore = useCurrentMemberStore()
  const { mealPlans: repo } = useRepositories()

  const { data, pending, error, refresh } = useAsyncData<MyMeal[]>(
    () => `my-meals-${tenantStore.currentTenantId}-${memberStore.linkedMemberId ?? 'none'}`,
    () => {
      const tenantId = tenantStore.currentTenantId
      const memberId = memberStore.linkedMemberId
      if (!tenantId || !memberId) return Promise.resolve([])
      return repo.listMyMeals(tenantId, memberId, today())
    },
    { watch: [() => tenantStore.currentTenantId, () => memberStore.linkedMemberId] },
  )

  return { meals: computed(() => data.value ?? []), pending, error, refresh }
}

/** Shopping items the current member has claimed but not yet provided. */
export function useMyShopping() {
  const tenantStore = useTenantStore()
  const memberStore = useCurrentMemberStore()
  const { shoppingItems: repo } = useRepositories()

  const { data, pending, error, refresh } = useAsyncData<ShoppingItem[]>(
    () => `my-shopping-${tenantStore.currentTenantId}-${memberStore.linkedMemberId ?? 'none'}`,
    () => {
      const tenantId = tenantStore.currentTenantId
      const memberId = memberStore.linkedMemberId
      if (!tenantId || !memberId) return Promise.resolve([])
      return repo.listClaimedByMember(tenantId, memberId)
    },
    { watch: [() => tenantStore.currentTenantId, () => memberStore.linkedMemberId] },
  )

  // Sort by need-by date (nulls last) so the soonest item surfaces first.
  const items = computed(() =>
    [...(data.value ?? [])].sort((a, b) => (a.neededByDate ?? '9999').localeCompare(b.neededByDate ?? '9999')))

  return { items, pending, error, refresh }
}
