import type { TenantRole } from '~/repositories/types'
import { useTenantStore } from '~/stores/tenant'
import { useCurrentMemberStore } from '~/stores/member'
import { useEventStore } from '~/stores/event'

export function useTenantSelect() {
  const tenantStore = useTenantStore()
  const memberStore = useCurrentMemberStore()
  const eventStore = useEventStore()
  const lastTenantId = useCookie('last_tenant_id')

  function clearStaleContext(incomingId: string) {
    if (incomingId !== tenantStore.currentTenantId) {
      memberStore.clear()
      eventStore.clear()
    }
  }

  function selectTenant(tenant: { id: string; name: string; userRole: TenantRole | null }) {
    clearStaleContext(tenant.id)
    lastTenantId.value = tenant.id
    tenantStore.setTenant(tenant.id, tenant.name, tenant.userRole)
    navigateTo('/app')
  }

  // Used when only the tenant ID is available (e.g. deep-link redirect).
  // Name/role will be populated by middleware once /app loads.
  function switchToTenantId(tenantId: string) {
    clearStaleContext(tenantId)
    lastTenantId.value = tenantId
    navigateTo('/app', { replace: true })
  }

  return { selectTenant, switchToTenantId }
}
