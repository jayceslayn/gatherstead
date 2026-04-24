import { useTenantStore } from '~/stores/tenant'
import type { HouseholdMember, DietaryProfile } from '~/repositories/types'
import { useRepositories } from '~/composables/useRepositories'

export type { HouseholdMember, DietaryProfile }

export function useAllMembers() {
  const tenantStore = useTenantStore()
  const { households: householdRepo, householdMembers: memberRepo } = useRepositories()

  const { data, pending } = useAsyncData<Map<string, HouseholdMember>>(
    () => `all-members-${tenantStore.currentTenantId}`,
    async () => {
      const households = await householdRepo.listHouseholds(tenantStore.currentTenantId!)
      const arrays = await Promise.all(
        households.map(h => memberRepo.listMembers(tenantStore.currentTenantId!, h.id)),
      )
      return new Map(arrays.flat().map(m => [m.id, m]))
    },
    { watch: [() => tenantStore.currentTenantId] },
  )

  return {
    memberMap: computed(() => data.value ?? new Map<string, HouseholdMember>()),
    pending,
  }
}

export function useHouseholdMembers(householdId: Ref<string>) {
  const tenantStore = useTenantStore()
  const { householdMembers: repo } = useRepositories()

  const { data, pending, error, refresh } = useAsyncData<HouseholdMember[]>(
    () => `members-${tenantStore.currentTenantId}-${householdId.value}`,
    () => repo.listMembers(tenantStore.currentTenantId!, householdId.value),
    { watch: [householdId] },
  )

  return { members: computed(() => data.value ?? []), pending, error, refresh }
}

export function useMember(householdId: Ref<string>, memberId: Ref<string>) {
  const tenantStore = useTenantStore()
  const { householdMembers: repo } = useRepositories()

  const { data, pending, error } = useAsyncData<HouseholdMember | null>(
    () => `member-${tenantStore.currentTenantId}-${householdId.value}-${memberId.value}`,
    () => repo.getMember(tenantStore.currentTenantId!, householdId.value, memberId.value),
    { watch: [householdId, memberId] },
  )

  return { member: computed(() => data.value ?? null), pending, error }
}

export function useDietaryProfile(householdId: Ref<string>, memberId: Ref<string>) {
  const tenantStore = useTenantStore()
  const { householdMembers: repo } = useRepositories()

  const { data, pending, error } = useAsyncData<DietaryProfile | null>(
    () => `dietary-${tenantStore.currentTenantId}-${householdId.value}-${memberId.value}`,
    () => repo.getDietaryProfile(tenantStore.currentTenantId!, householdId.value, memberId.value),
    { watch: [householdId, memberId] },
  )

  return { dietaryProfile: computed(() => data.value ?? null), pending, error }
}
