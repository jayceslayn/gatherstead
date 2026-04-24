import { useTenantStore } from '~/stores/tenant'
import type { PropertySummary } from '~/repositories/types'
import { useRepositories } from '~/composables/useRepositories'

export type { PropertySummary }

export function useProperties() {
  const tenantStore = useTenantStore()
  const { properties: repo } = useRepositories()

  const { data, pending, error, refresh } = useAsyncData<PropertySummary[]>(
    () => `properties-${tenantStore.currentTenantId}`,
    () => repo.listProperties(tenantStore.currentTenantId!),
    { watch: [() => tenantStore.currentTenantId] },
  )

  return { properties: computed(() => data.value ?? []), pending, error, refresh }
}

export function useProperty(propertyId: Ref<string>) {
  const tenantStore = useTenantStore()
  const { properties: repo } = useRepositories()

  const { data, pending, error } = useAsyncData<PropertySummary | null>(
    () => `property-${tenantStore.currentTenantId}-${propertyId.value}`,
    () => repo.getProperty(tenantStore.currentTenantId!, propertyId.value),
    { watch: [propertyId] },
  )

  return { property: computed(() => data.value ?? null), pending, error }
}
