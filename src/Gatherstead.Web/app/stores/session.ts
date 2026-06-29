import { defineStore } from 'pinia'

// Account-level session state, set once at bootstrap. Unlike the tenant store this does NOT clear
// on tenant switch: app-admin status is a property of the account, not the active tenant.
export const useSessionStore = defineStore('session', () => {
  // App admins act with full authority on any tenant (support/admin persona) while their tenant
  // role stays null so PII remains masked. Used purely as a UI affordance hint — the API enforces
  // both axes server-side regardless of this flag.
  const isAppAdmin = ref(false)

  function setAppAdmin(value: boolean) {
    isAppAdmin.value = value
  }

  function clear() {
    isAppAdmin.value = false
  }

  return { isAppAdmin, setAppAdmin, clear }
})
