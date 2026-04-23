import { useTenantStore } from '~/stores/tenant'
import type { HouseholdSummary } from '~/repositories/types'
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
