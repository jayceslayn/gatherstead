import { useTenantStore } from '~/stores/tenant'

export interface HouseholdMember {
  id: string
  tenantId: string
  householdId: string
  name: string
  isAdult: boolean
  ageBand: string | null
  birthDate: string | null
  dietaryNotes: string | null
  dietaryTags: string[]
}

export interface DietaryProfile {
  id: string
  tenantId: string
  householdMemberId: string
  preferredDiet: string
  allergies: string[]
  restrictions: string[]
  notes: string | null
}

interface MembersApiResponse {
  entity: HouseholdMember[]
  successful: boolean
}

interface MemberApiResponse {
  entity: HouseholdMember
  successful: boolean
}

interface DietaryProfileApiResponse {
  entity: DietaryProfile
  successful: boolean
}

export function useHouseholdMembers(householdId: Ref<string>) {
  const tenantStore = useTenantStore()
  const config = useRuntimeConfig()

  if (config.public.demoMode) {
    return {
      members: ref<HouseholdMember[]>([]),
      pending: ref(false),
      error: ref(null),
      refresh: () => Promise.resolve(),
    }
  }

  const { data, pending, error, refresh } = useAsyncData<HouseholdMember[]>(
    () => `members-${tenantStore.currentTenantId}-${householdId.value}`,
    async () => {
      const response = await $fetch<MembersApiResponse>(
        `/api/proxy/tenants/${tenantStore.currentTenantId}/households/${householdId.value}/members`,
      )
      return response.entity ?? []
    },
    { watch: [householdId] },
  )

  const members = computed(() => data.value ?? [])
  return { members, pending, error, refresh }
}

export function useMember(householdId: Ref<string>, memberId: Ref<string>) {
  const tenantStore = useTenantStore()
  const config = useRuntimeConfig()

  if (config.public.demoMode) {
    return {
      member: ref<HouseholdMember | null>(null),
      pending: ref(false),
      error: ref(null),
    }
  }

  const { data, pending, error } = useAsyncData<HouseholdMember>(
    () => `member-${tenantStore.currentTenantId}-${householdId.value}-${memberId.value}`,
    async () => {
      const response = await $fetch<MemberApiResponse>(
        `/api/proxy/tenants/${tenantStore.currentTenantId}/households/${householdId.value}/members/${memberId.value}`,
      )
      return response.entity
    },
    { watch: [householdId, memberId] },
  )

  const member = computed(() => data.value ?? null)
  return { member, pending, error }
}

export function useDietaryProfile(householdId: Ref<string>, memberId: Ref<string>) {
  const tenantStore = useTenantStore()
  const config = useRuntimeConfig()

  if (config.public.demoMode) {
    return {
      dietaryProfile: ref<DietaryProfile | null>(null),
      pending: ref(false),
      error: ref(null),
    }
  }

  const { data, pending, error } = useAsyncData<DietaryProfile | null>(
    () => `dietary-${tenantStore.currentTenantId}-${householdId.value}-${memberId.value}`,
    async () => {
      try {
        const response = await $fetch<DietaryProfileApiResponse>(
          `/api/proxy/tenants/${tenantStore.currentTenantId}/households/${householdId.value}/members/${memberId.value}/dietary-profile`,
        )
        return response.entity ?? null
      }
      catch {
        return null
      }
    },
    { watch: [householdId, memberId] },
  )

  const dietaryProfile = computed(() => data.value ?? null)
  return { dietaryProfile, pending, error }
}
