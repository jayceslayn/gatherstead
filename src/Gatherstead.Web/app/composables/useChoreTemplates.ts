import { useTenantStore } from '~/stores/tenant'

export type ChoreTimeSlot = 'Morning' | 'Midday' | 'Evening' | 'Anytime'

export interface ChoreTemplate {
  id: string
  tenantId: string
  eventId: string
  name: string
  timeSlots: number
  minimumAssignees: number | null
  notes: string | null
}

export interface ChorePlan {
  id: string
  tenantId: string
  templateId: string
  day: string
  timeSlot: ChoreTimeSlot | null
  completed: boolean
  notes: string | null
  isException: boolean
  exceptionReason: string | null
}

export interface ChoreIntent {
  id: string
  tenantId: string
  chorePlanId: string
  householdMemberId: string
  volunteered: boolean
}

const CHORE_SLOT_FLAGS: Record<ChoreTimeSlot, number> = {
  Morning: 0x01,
  Midday: 0x02,
  Evening: 0x04,
  Anytime: 0x08,
}

export const ALL_CHORE_SLOTS: ChoreTimeSlot[] = ['Morning', 'Midday', 'Evening', 'Anytime']

export function choreSlotsFromFlags(flags: number): ChoreTimeSlot[] {
  return ALL_CHORE_SLOTS.filter(s => (flags & CHORE_SLOT_FLAGS[s]) !== 0)
}

interface ApiResponse<T> { entity: T; successful: boolean }

export function useChoreTemplates(eventId: Ref<string>) {
  const tenantStore = useTenantStore()
  const config = useRuntimeConfig()

  if (config.public.demoMode) {
    return { templates: ref<ChoreTemplate[]>([]), pending: ref(false), error: ref(null) }
  }

  const { data, pending, error } = useAsyncData<ChoreTemplate[]>(
    () => `chore-templates-${tenantStore.currentTenantId}-${eventId.value}`,
    async () => {
      const res = await $fetch<ApiResponse<ChoreTemplate[]>>(
        `/api/proxy/tenants/${tenantStore.currentTenantId}/events/${eventId.value}/chore-templates`,
      )
      return res.entity ?? []
    },
    { watch: [eventId] },
  )

  return { templates: computed(() => data.value ?? []), pending, error }
}

export function useChorePlanSection(
  eventId: Ref<string>,
  templateId: Ref<string>,
  memberId: Ref<string | null>,
  householdId: Ref<string | null>,
) {
  const tenantStore = useTenantStore()
  const config = useRuntimeConfig()

  if (config.public.demoMode) {
    return {
      plans: ref<ChorePlan[]>([]),
      intentMap: ref<Record<string, ChoreIntent | null>>({}),
      pending: ref(false),
      updating: ref<string[]>([]),
      toggle: async (_planId: string) => {},
    }
  }

  const { data: plansData, pending: plansPending } = useAsyncData<ChorePlan[]>(
    () => `chore-plans-${tenantStore.currentTenantId}-${eventId.value}-${templateId.value}`,
    async () => {
      const res = await $fetch<ApiResponse<ChorePlan[]>>(
        `/api/proxy/tenants/${tenantStore.currentTenantId}/events/${eventId.value}/chore-templates/${templateId.value}/plans`,
      )
      return res.entity ?? []
    },
    { watch: [eventId, templateId] },
  )

  const plans = computed(() => plansData.value ?? [])
  const intentMap = ref<Record<string, ChoreIntent | null>>({})
  const intentsPending = ref(false)
  const updating = ref<string[]>([])

  async function loadIntents() {
    if (!memberId.value || !plans.value.length) {
      intentMap.value = {}
      return
    }
    intentsPending.value = true
    try {
      const entries = await Promise.all(
        plans.value.map(async (plan) => {
          try {
            const res = await $fetch<ApiResponse<ChoreIntent[]>>(
              `/api/proxy/tenants/${tenantStore.currentTenantId}/events/${eventId.value}/chore-templates/${templateId.value}/plans/${plan.id}/intents?memberIds=${encodeURIComponent(memberId.value!)}`,
            )
            return [plan.id, res.entity?.find(i => i.householdMemberId === memberId.value) ?? null] as [string, ChoreIntent | null]
          }
          catch {
            return [plan.id, null] as [string, ChoreIntent | null]
          }
        }),
      )
      intentMap.value = Object.fromEntries(entries)
    }
    finally {
      intentsPending.value = false
    }
  }

  watch([plans, memberId], () => loadIntents(), { immediate: true })

  async function toggle(planId: string) {
    if (!memberId.value || !householdId.value) return
    const current = intentMap.value[planId]
    const newValue = !current?.volunteered
    updating.value = [...updating.value, planId]
    try {
      await $fetch(
        `/api/proxy/tenants/${tenantStore.currentTenantId}/events/${eventId.value}/chore-templates/${templateId.value}/plans/${planId}/intents?householdId=${encodeURIComponent(householdId.value)}`,
        {
          method: 'PUT',
          body: { householdMemberId: memberId.value, volunteered: newValue },
        },
      )
      try {
        const res = await $fetch<ApiResponse<ChoreIntent[]>>(
          `/api/proxy/tenants/${tenantStore.currentTenantId}/events/${eventId.value}/chore-templates/${templateId.value}/plans/${planId}/intents?memberIds=${encodeURIComponent(memberId.value)}`,
        )
        intentMap.value[planId] = res.entity?.find(i => i.householdMemberId === memberId.value) ?? null
      }
      catch {
        intentMap.value[planId] = null
      }
    }
    finally {
      updating.value = updating.value.filter(id => id !== planId)
    }
  }

  const pending = computed(() => plansPending.value || intentsPending.value)
  return { plans, intentMap, pending, updating, toggle }
}
