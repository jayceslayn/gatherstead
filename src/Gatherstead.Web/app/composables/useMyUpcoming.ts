import { useTenantStore } from '~/stores/tenant'
import { useCurrentMemberStore } from '~/stores/member'
import { useRepositories } from '~/composables/useRepositories'
import type { MyStay, MyTask, ShoppingItem } from '~/repositories/types'

function today(): string {
  return new Date().toISOString().substring(0, 10)
}

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
