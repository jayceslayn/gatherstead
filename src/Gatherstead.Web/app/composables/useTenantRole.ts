import { useTenantStore } from '~/stores/tenant'
import { useSessionStore } from '~/stores/session'
import type { HouseholdSummary } from '~/repositories/types'

export function useTenantRole() {
  const tenantStore = useTenantStore()
  const sessionStore = useSessionStore()
  const role = computed(() => tenantStore.currentUserRole)
  const isAppAdmin = computed(() => sessionStore.isAppAdmin)

  // App admins act with full authority on any tenant (support/admin persona), so they satisfy every
  // role threshold here. Their tenant `role` stays null, keeping PII masked on the visibility axis —
  // the API enforces both axes regardless of these UI hints.
  const isOwner = computed(() => isAppAdmin.value || role.value === 'Owner')
  const isManagerOrAbove = computed(() => isAppAdmin.value || role.value === 'Owner' || role.value === 'Manager')
  const isCoordinatorOrAbove = computed(() => isAppAdmin.value || role.value === 'Owner' || role.value === 'Manager' || role.value === 'Coordinator')
  const isMemberOrAbove = computed(() => isAppAdmin.value || (role.value !== null && role.value !== 'Guest'))

  // "Household manager" for UI gating: a tenant Manager/above, or a Manager of *this* specific
  // household via a per-household grant (which can outrank the caller's tenant role). Mirrors the
  // API's CanManageHousehold / CanEditMember checks, which honour both axes. Compose with an
  // isSelf check where the API also permits self-edit.
  const canManageHousehold = (household: Ref<HouseholdSummary | null>) =>
    computed(() => isManagerOrAbove.value || household.value?.callerRole === 'Manager')

  return { role, isAppAdmin, isOwner, isManagerOrAbove, isCoordinatorOrAbove, isMemberOrAbove, canManageHousehold }
}
