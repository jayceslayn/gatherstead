export type TenantRole = 'Owner' | 'Manager' | 'Member' | 'Guest'

export interface TenantSummary {
  id: string
  name: string
  userRole: TenantRole | null
}

interface TenantsApiResponse {
  entity: Array<{ id: string; name: string; userRole: TenantRole | null }>
  successful: boolean
}

export function useTenants() {
  const config = useRuntimeConfig()

  if (config.public.demoMode) {
    return {
      tenants: ref<TenantSummary[]>([]),
      pending: ref(false),
      error: ref(null),
      refresh: () => Promise.resolve(),
    }
  }

  const { data, pending, error, refresh } = useAsyncData<TenantSummary[]>(
    'tenants',
    async () => {
      const response = await $fetch<TenantsApiResponse>('/api/proxy/tenants')
      return (response.entity ?? []).map(t => ({
        id: t.id,
        name: t.name,
        userRole: t.userRole,
      }))
    },
  )

  const tenants = computed(() => data.value ?? [])

  return { tenants, pending, error, refresh }
}
