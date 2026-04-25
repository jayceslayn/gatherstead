import { useTenantStore } from '~/stores/tenant'
import type { HouseholdSummary } from '~/repositories/types'
import { DemoLimitError } from '~/repositories/interfaces'
import { useRepositories } from '~/composables/useRepositories'

export type { HouseholdSummary }

export function useHouseholds() {
  const tenantStore = useTenantStore()
  const { households: repo } = useRepositories()

  const { data, pending, error, refresh } = useAsyncData<HouseholdSummary[]>(
    () => `households-${tenantStore.currentTenantId}`,
    () => repo.listHouseholds(tenantStore.currentTenantId!),
    { watch: [() => tenantStore.currentTenantId] },
  )

  return { households: computed(() => data.value ?? []), pending, error, refresh }
}

export function useHousehold(householdId: Ref<string>) {
  const tenantStore = useTenantStore()
  const { households: repo } = useRepositories()

  const { data, pending, error } = useAsyncData<HouseholdSummary | null>(
    () => `household-${tenantStore.currentTenantId}-${householdId.value}`,
    () => repo.getHousehold(tenantStore.currentTenantId!, householdId.value),
    { watch: [householdId] },
  )

  return { household: computed(() => data.value ?? null), pending, error }
}

export function useHouseholdActions(refresh: () => Promise<void>) {
  const tenantStore = useTenantStore()
  const { households: repo } = useRepositories()
  const toast = useToast()
  const { t } = useI18n()
  const { translateError } = useApiError()
  const updating = ref<string[]>([])

  async function createHousehold(name: string) {
    updating.value.push('new')
    try {
      await repo.createHousehold(tenantStore.currentTenantId!, name)
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

  async function updateHousehold(householdId: string, name: string) {
    updating.value.push(householdId)
    try {
      await repo.updateHousehold(tenantStore.currentTenantId!, householdId, name)
      await refresh()
    }
    catch (e) {
      toast.add({ title: translateError(e as { code: string }), color: 'error' })
    }
    finally {
      updating.value = updating.value.filter(k => k !== householdId)
    }
  }

  async function deleteHousehold(householdId: string) {
    updating.value.push(householdId)
    try {
      await repo.deleteHousehold(tenantStore.currentTenantId!, householdId)
      await refresh()
    }
    catch (e) {
      toast.add({ title: translateError(e as { code: string }), color: 'error' })
    }
    finally {
      updating.value = updating.value.filter(k => k !== householdId)
    }
  }

  return { updating, createHousehold, updateHousehold, deleteHousehold }
}
