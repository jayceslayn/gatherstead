import { useTenantStore } from '~/stores/tenant'
import type { HouseholdMember, HouseholdRole, DietaryProfile } from '~/repositories/types'
import { DemoLimitError } from '~/repositories/interfaces'
import { useRepositories } from '~/composables/useRepositories'

export type { HouseholdMember, HouseholdRole, DietaryProfile }

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

export function useHouseholdMemberActions(householdId: Ref<string>, refresh: () => Promise<void>) {
  const tenantStore = useTenantStore()
  const { householdMembers: repo } = useRepositories()
  const toast = useToast()
  const { t } = useI18n()
  const { translateError } = useApiError()
  const updating = ref<string[]>([])

  async function createMember(
    name: string,
    isAdult: boolean,
    ageBand: string | null,
    birthDate: string | null,
    householdRole: HouseholdRole,
    dietaryNotes: string | null,
    dietaryTags: string[],
  ) {
    updating.value.push('new')
    try {
      await repo.createMember(
        tenantStore.currentTenantId!, householdId.value,
        name, isAdult, ageBand, birthDate, householdRole, dietaryNotes, dietaryTags,
      )
      await refresh()
    }
    catch (e) {
      if (e instanceof DemoLimitError) {
        toast.add({ title: t('demo.limitReached.title'), description: t('demo.limitReached.description'), color: 'warning' })
        return
      }
      toast.add({ title: translateError(e as { code: string }), color: 'error' })
    }
    finally {
      updating.value = updating.value.filter(k => k !== 'new')
    }
  }

  async function updateMember(
    memberId: string,
    name: string,
    isAdult: boolean,
    ageBand: string | null,
    birthDate: string | null,
    householdRole: HouseholdRole,
    dietaryNotes: string | null,
    dietaryTags: string[],
  ) {
    updating.value.push(memberId)
    try {
      await repo.updateMember(
        tenantStore.currentTenantId!, householdId.value, memberId,
        name, isAdult, ageBand, birthDate, householdRole, dietaryNotes, dietaryTags,
      )
      await refresh()
    }
    catch (e) {
      toast.add({ title: translateError(e as { code: string }), color: 'error' })
    }
    finally {
      updating.value = updating.value.filter(k => k !== memberId)
    }
  }

  async function deleteMember(memberId: string) {
    updating.value.push(memberId)
    try {
      await repo.deleteMember(tenantStore.currentTenantId!, householdId.value, memberId)
      await refresh()
    }
    catch (e) {
      toast.add({ title: translateError(e as { code: string }), color: 'error' })
    }
    finally {
      updating.value = updating.value.filter(k => k !== memberId)
    }
  }

  return { updating, createMember, updateMember, deleteMember }
}
