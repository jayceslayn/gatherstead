import { useTenantStore } from '~/stores/tenant'
import { useSessionStore } from '~/stores/session'

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

  return { role, isAppAdmin, isOwner, isManagerOrAbove, isCoordinatorOrAbove, isMemberOrAbove }
}
