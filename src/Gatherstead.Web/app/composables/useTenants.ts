import type { TenantSummary, TenantRole } from '~/repositories/types'
import { useRepositories } from '~/composables/useRepositories'

export type { TenantSummary, TenantRole }

export function useTenants() {
  const { tenants: repo } = useRepositories()

  const { data, pending, error, refresh } = useAsyncData<TenantSummary[]>(
    'tenants',
    () => repo.listTenants(),
  )

  return { tenants: computed(() => data.value ?? []), pending, error, refresh }
}
