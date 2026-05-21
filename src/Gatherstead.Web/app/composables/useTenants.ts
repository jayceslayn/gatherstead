import type { TenantSummary } from '~/repositories/types'
import { useRepositories } from '~/composables/useRepositories'


export function useTenants() {
  const { tenants: repo } = useRepositories()

  const { data, pending, error, refresh } = useAsyncData<TenantSummary[]>(
    'tenants',
    () => repo.listTenants(),
  )

  return { tenants: computed(() => data.value ?? []), pending, error, refresh }
}
