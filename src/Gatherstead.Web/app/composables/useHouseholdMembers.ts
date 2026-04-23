import { useTenantStore } from '~/stores/tenant'
import type { HouseholdMember, DietaryProfile } from '~/repositories/types'
import { useRepositories } from '~/composables/useRepositories'

export type { HouseholdMember, DietaryProfile }

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
