import { defineStore } from 'pinia'

export const useCurrentMemberStore = defineStore('currentMember', () => {
  const linkedMemberId = ref<string | null>(null)
  const linkedHouseholdId = ref<string | null>(null)
  const activeTenantId = ref<string | null>(null)

  function setLinkedMember(memberId: string, householdId: string) {
    linkedMemberId.value = memberId
    linkedHouseholdId.value = householdId
  }

  // Marks the store as loaded for a given tenant and clears any prior member link.
  // Call this before populating member data on tenant change.
  function clearForTenant(tenantId: string) {
    activeTenantId.value = tenantId
    linkedMemberId.value = null
    linkedHouseholdId.value = null
  }

  function clear() {
    activeTenantId.value = null
    linkedMemberId.value = null
    linkedHouseholdId.value = null
  }

  return { linkedMemberId, linkedHouseholdId, activeTenantId, setLinkedMember, clearForTenant, clear }
})
