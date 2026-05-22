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

export function useHouseholdUserActions(householdId: Ref<string>, refresh?: () => Promise<void>) {
  const tenantStore = useTenantStore()
  const { tenantUsers: repo } = useRepositories()
  const toast = useToast()
  const { translateError } = useApiError()
  const updating = ref<string[]>([])

  async function upsertHouseholdUser(userId: string, role: HouseholdRole) {
    updating.value.push(userId)
    try {
      await repo.upsertHouseholdUser(tenantStore.currentTenantId!, householdId.value, userId, role)
      await refresh?.()
    }
    catch (e) {
      toast.add({ title: translateError(e as { code: string }), color: 'error' })
    }
    finally {
      updating.value = updating.value.filter(k => k !== userId)
    }
  }

  async function deleteHouseholdUser(userId: string) {
    updating.value.push(userId)
    try {
      await repo.deleteHouseholdUser(tenantStore.currentTenantId!, householdId.value, userId)
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
