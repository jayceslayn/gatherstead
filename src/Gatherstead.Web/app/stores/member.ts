import { defineStore } from 'pinia'

export const useCurrentMemberStore = defineStore('currentMember', () => {
  const linkedMemberId = ref<string | null>(null)
  const linkedHouseholdId = ref<string | null>(null)

  function setLinkedMember(memberId: string, householdId: string) {
    linkedMemberId.value = memberId
    linkedHouseholdId.value = householdId
  }

  function clear() {
    linkedMemberId.value = null
    linkedHouseholdId.value = null
  }

  return { linkedMemberId, linkedHouseholdId, setLinkedMember, clear }
})
