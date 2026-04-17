export interface TenantSummary {
  id: string
  name: string
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

  const { data: tenants, pending, error, refresh } = useAsyncData<TenantSummary[]>(
    'tenants',
    () => $fetch<TenantSummary[]>('/api/proxy/tenants'),
  )

  return { tenants, pending, error, refresh }
}
