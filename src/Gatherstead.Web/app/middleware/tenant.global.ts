import { useTenantStore } from '~/stores/tenant'
import { useCurrentMemberStore } from '~/stores/member'
import { useSessionStore } from '~/stores/session'
import type { TenantRole } from '~/repositories/types'
import { DEMO_TENANT, DEMO_USER } from '~/repositories/demo/demoConstants'

interface TenantsApiResponse {
  entity: Array<{ id: string; name: string; userRole: TenantRole | null }>
  successful: boolean
}

interface TenantUserMeApiResponse {
  entity: { linkedMemberId: string | null, linkedHouseholdId: string | null }
}

interface BootstrapApiResponse {
  entity: { userId: string, isAppAdmin: boolean, claimedInvitations: number }
}

// Provision the internal user and claim any pending invitations exactly once per page load,
// before tenants are resolved, so a freshly-invited user's membership is already in place.
let bootstrapPromise: Promise<BootstrapApiResponse | null> | null = null
function ensureBootstrap() {
  if (!bootstrapPromise) {
    bootstrapPromise = $fetch<BootstrapApiResponse>('/api/proxy/me/bootstrap', { method: 'POST' })
      .catch(() => null)
  }
  return bootstrapPromise
}

export default defineNuxtRouteMiddleware(async (to) => {
  // /app/* is tenant-scoped; /user/* is the caller's own account (not tenant-scoped) but still
  // needs the login guard + bootstrap so /api/me is provisioned.
  const isApp = to.path.startsWith('/app')
  const isUser = to.path.startsWith('/user')
  if (!isApp && !isUser) return

  const tenantStore = useTenantStore()
  const memberStore = useCurrentMemberStore()
  const sessionStore = useSessionStore()

  if (__DEMO_MODE__) {
    // User routes don't need tenant/member context in demo mode — the demo `me` repository
    // returns a profile on its own.
    if (isUser) return
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
  // The bootstrap response also carries the account-level app-admin flag.
  const bootstrap = await ensureBootstrap()
  sessionStore.setAppAdmin(bootstrap?.entity?.isAppAdmin === true)

  // The remainder is tenant context, which user-scoped routes don't need.
  if (isUser) return

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
