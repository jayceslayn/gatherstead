import { useTenantStore } from '~/stores/tenant'

export function useTenantRole() {
  const tenantStore = useTenantStore()
  const role = computed(() => tenantStore.currentUserRole)

  const isOwner = computed(() => role.value === 'Owner')
  const isManagerOrAbove = computed(() => role.value === 'Owner' || role.value === 'Manager')
  const isCoordinatorOrAbove = computed(() => role.value === 'Owner' || role.value === 'Manager' || role.value === 'Coordinator')
  const isMemberOrAbove = computed(() => role.value !== null && role.value !== 'Guest')

  return { role, isOwner, isManagerOrAbove, isCoordinatorOrAbove, isMemberOrAbove }
}
