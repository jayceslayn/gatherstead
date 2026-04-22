import { useTenantStore } from '~/stores/tenant'

export interface HouseholdSummary {
  id: string
  tenantId: string
  name: string
}

interface HouseholdsApiResponse {
  entity: HouseholdSummary[]
  successful: boolean
}

interface HouseholdApiResponse {
  entity: HouseholdSummary
  successful: boolean
}

export function useHouseholds() {
  const tenantStore = useTenantStore()
  const config = useRuntimeConfig()

  if (config.public.demoMode) {
    return {
      households: ref<HouseholdSummary[]>([]),
      pending: ref(false),
      error: ref(null),
      refresh: () => Promise.resolve(),
    }
  }

  const { data, pending, error, refresh } = useAsyncData<HouseholdSummary[]>(
    () => `households-${tenantStore.currentTenantId}`,
    async () => {
      const response = await $fetch<HouseholdsApiResponse>(
        `/api/proxy/tenants/${tenantStore.currentTenantId}/households`,
      )
      return response.entity ?? []
    },
    { watch: [() => tenantStore.currentTenantId] },
  )

  const households = computed(() => data.value ?? [])
  return { households, pending, error, refresh }
}

export function useHousehold(householdId: Ref<string>) {
  const tenantStore = useTenantStore()
  const config = useRuntimeConfig()

  if (config.public.demoMode) {
    return {
      household: ref<HouseholdSummary | null>(null),
      pending: ref(false),
      error: ref(null),
    }
  }

  const { data, pending, error } = useAsyncData<HouseholdSummary>(
    () => `household-${tenantStore.currentTenantId}-${householdId.value}`,
    async () => {
      const response = await $fetch<HouseholdApiResponse>(
        `/api/proxy/tenants/${tenantStore.currentTenantId}/households/${householdId.value}`,
      )
      return response.entity
    },
    { watch: [householdId] },
  )

  const household = computed(() => data.value ?? null)
  return { household, pending, error }
}
