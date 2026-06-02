import { useTenantStore } from '~/stores/tenant'
import { useCurrentMemberStore } from '~/stores/member'
import type { TenantRole } from '~/repositories/types'
import { DEMO_TENANT, DEMO_USER } from '~/repositories/demo/demoConstants'

interface TenantsApiResponse {
  entity: Array<{ id: string; name: string; userRole: TenantRole | null }>
  successful: boolean
}

interface TenantUserMeApiResponse {
  entity: { linkedMemberId: string | null, linkedHouseholdId: string | null }
}

// Provision the internal user and claim any pending invitations exactly once per page load,
// before tenants are resolved, so a freshly-invited user's membership is already in place.
let bootstrapPromise: Promise<unknown> | null = null
function ensureBootstrap() {
  if (!bootstrapPromise) {
    bootstrapPromise = $fetch('/api/proxy/me/bootstrap', { method: 'POST' }).catch(() => null)
  }
  return bootstrapPromise
}

export default defineNuxtRouteMiddleware(async (to) => {
  if (!to.path.startsWith('/app')) return

  const tenantStore = useTenantStore()
  const memberStore = useCurrentMemberStore()

  if (__DEMO_MODE__) {
    if (!tenantStore.currentTenantId) {
      tenantStore.setTenant(DEMO_TENANT.id, DEMO_TENANT.name, DEMO_TENANT.userRole)
    }
    if (memberStore.activeTenantId !== DEMO_TENANT.id) {
      memberStore.clearForTenant(DEMO_TENANT.id)
      const { getDemoStore } = await import('~/repositories/demo/DemoStore')
      const demoStore = getDemoStore()
      const demoUser = demoStore.tenantUsers.value.find(u => u.userId === DEMO_USER.userId)
      if (demoUser?.linkedMemberId) {
        const linkedMember = demoStore.members.value.find(m => m.id === demoUser.linkedMemberId)
        if (linkedMember) {
          memberStore.setLinkedMember(linkedMember.id, linkedMember.householdId)
        }
      }
    }
    return
  }

  const { loggedIn } = useAuth()
  if (!loggedIn.value) {
    return navigateTo('/')
  }

  // Live mode: provision the internal user and claim any pending invitations before
  // resolving tenants, so a freshly-invited user's membership is already in place.
  await ensureBootstrap()

  // Live mode: resolve tenant on first load
  if (!tenantStore.currentTenantId) {
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
  }

  // Hydrate member store when tenant changes (or on first load)
  if (memberStore.activeTenantId !== tenantStore.currentTenantId) {
    memberStore.clearForTenant(tenantStore.currentTenantId!)
    try {
      const me = await $fetch<TenantUserMeApiResponse>(
        `/api/proxy/tenants/${tenantStore.currentTenantId}/users/me`,
      )
      if (me.entity.linkedMemberId && me.entity.linkedHouseholdId) {
        memberStore.setLinkedMember(me.entity.linkedMemberId, me.entity.linkedHouseholdId)
      }
    }
    catch {
      // Leave store cleared — user profile link won't be shown
    }
  }
})
