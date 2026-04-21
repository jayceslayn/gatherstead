import { defineStore } from 'pinia'
import type { TenantRole } from '~/composables/useTenants'

export const useTenantStore = defineStore('tenant', () => {
  const currentTenantId = ref<string | null>(null)
  const currentTenantName = ref<string | null>(null)
  const currentUserRole = ref<TenantRole | null>(null)

  function setTenant(id: string, name: string, role: TenantRole | null = null) {
    currentTenantId.value = id
    currentTenantName.value = name
    currentUserRole.value = role
  }

  function setUserRole(role: TenantRole | null) {
    currentUserRole.value = role
  }

  function clear() {
    currentTenantId.value = null
    currentTenantName.value = null
    currentUserRole.value = null
  }

  return { currentTenantId, currentTenantName, currentUserRole, setTenant, setUserRole, clear }
})
