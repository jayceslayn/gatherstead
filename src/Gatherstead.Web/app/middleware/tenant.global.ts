import { useTenantStore } from '~/stores/tenant'
import type { TenantRole } from '~/composables/useTenants'

interface TenantsApiResponse {
  entity: Array<{ id: string; name: string; userRole: TenantRole | null }>
  successful: boolean
}

export default defineNuxtRouteMiddleware(async (to) => {
  if (!to.path.startsWith('/app')) return

  const config = useRuntimeConfig()

  if (!config.public.demoMode) {
    const { loggedIn } = useAuth()
    if (!loggedIn.value) {
      return navigateTo('/')
    }
  }

  const tenantStore = useTenantStore()
  if (tenantStore.currentTenantId) return

  const lastTenantId = useCookie('last_tenant_id')
  if (!lastTenantId.value) {
    return navigateTo('/tenants')
  }

  try {
    const response = await $fetch<TenantsApiResponse>('/api/proxy/tenants')
    const tenant = response.entity?.find(t => t.id === lastTenantId.value)
    if (!tenant) {
      return navigateTo('/tenants')
    }
    tenantStore.setTenant(tenant.id, tenant.name, tenant.userRole)
  }
  catch {
    return navigateTo('/tenants')
  }
})
