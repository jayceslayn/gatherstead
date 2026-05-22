import { useTenantStore } from '~/stores/tenant'
import { useRepositories } from '~/composables/useRepositories'
import type { HouseholdRole } from '~/repositories/types'

export function useHouseholdUsers(householdId: Ref<string>) {
  const tenantStore = useTenantStore()
  const { tenantUsers: repo } = useRepositories()
  const { data, pending, error, refresh } = useAsyncData(
    () => `household-users-${tenantStore.currentTenantId}-${householdId.value}`,
    () => repo.listHouseholdUsers(tenantStore.currentTenantId!, householdId.value),
    { watch: [householdId] },
  )
  return { householdUsers: computed(() => data.value ?? []), pending, error, refresh }
}

export function useUserHouseholdAccess(userId: Ref<string>) {
  const tenantStore = useTenantStore()
  const { tenantUsers: repo } = useRepositories()
  const { data, pending, error, refresh } = useAsyncData(
    () => `user-household-access-${tenantStore.currentTenantId}-${userId.value}`,
    () => userId.value ? repo.listUserHouseholdAccess(tenantStore.currentTenantId!, userId.value) : Promise.resolve([]),
    { watch: [userId] },
  )
  return { access: computed(() => data.value ?? []), pending, error, refresh }
}

export function useHouseholdUserActions(refresh?: () => Promise<void>) {
  const tenantStore = useTenantStore()
  const { tenantUsers: repo } = useRepositories()
  const toast = useToast()
  const { translateError } = useApiError()
  const updating = ref<string[]>([])

  async function upsertHouseholdUser(householdId: string, userId: string, role: HouseholdRole) {
    updating.value.push(userId)
    try {
      await repo.upsertHouseholdUser(tenantStore.currentTenantId!, householdId, userId, role)
      await refresh?.()
    }
    catch (e) {
      toast.add({ title: translateError(e as { code: string }), color: 'error' })
    }
    finally {
      updating.value = updating.value.filter(k => k !== userId)
    }
  }

  async function deleteHouseholdUser(householdId: string, userId: string) {
    updating.value.push(userId)
    try {
      await repo.deleteHouseholdUser(tenantStore.currentTenantId!, householdId, userId)
      await refresh?.()
    }
    catch (e) {
      toast.add({ title: translateError(e as { code: string }), color: 'error' })
    }
    finally {
      updating.value = updating.value.filter(k => k !== userId)
    }
  }

  return { updating, upsertHouseholdUser, deleteHouseholdUser }
}
