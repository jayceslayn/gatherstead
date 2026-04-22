import { useTenantStore } from '~/stores/tenant'

export type MealType = 'Breakfast' | 'Lunch' | 'Dinner'
export type MealIntentStatus = 'Going' | 'Maybe' | 'NotGoing'

export interface MealTemplate {
  id: string
  tenantId: string
  eventId: string
  name: string
  mealTypes: number
  notes: string | null
}

export interface MealPlan {
  id: string
  tenantId: string
  mealTemplateId: string
  day: string
  mealType: MealType
  notes: string | null
  isException: boolean
  exceptionReason: string | null
}

export interface MealIntent {
  id: string
  tenantId: string
  mealPlanId: string
  householdMemberId: string
  status: MealIntentStatus
  bringOwnFood: boolean
  notes: string | null
}

const MEAL_TYPE_FLAGS: Record<MealType, number> = {
  Breakfast: 0x01,
  Lunch: 0x02,
  Dinner: 0x04,
}

export const ALL_MEAL_TYPES: MealType[] = ['Breakfast', 'Lunch', 'Dinner']

export function mealTypesFromFlags(flags: number): MealType[] {
  return ALL_MEAL_TYPES.filter(t => (flags & MEAL_TYPE_FLAGS[t]) !== 0)
}

interface ApiResponse<T> { entity: T; successful: boolean }

export function useMealTemplates(eventId: Ref<string>) {
  const tenantStore = useTenantStore()
  const config = useRuntimeConfig()

  if (config.public.demoMode) {
    return { templates: ref<MealTemplate[]>([]), pending: ref(false), error: ref(null) }
  }

  const { data, pending, error } = useAsyncData<MealTemplate[]>(
    () => `meal-templates-${tenantStore.currentTenantId}-${eventId.value}`,
    async () => {
      const res = await $fetch<ApiResponse<MealTemplate[]>>(
        `/api/proxy/tenants/${tenantStore.currentTenantId}/events/${eventId.value}/meal-templates`,
      )
      return res.entity ?? []
    },
    { watch: [eventId] },
  )

  return { templates: computed(() => data.value ?? []), pending, error }
}

export function useMealPlanSection(
  eventId: Ref<string>,
  templateId: Ref<string>,
  memberId: Ref<string | null>,
  householdId: Ref<string | null>,
) {
  const tenantStore = useTenantStore()
  const config = useRuntimeConfig()

  if (config.public.demoMode) {
    return {
      plans: ref<MealPlan[]>([]),
      intentMap: ref<Record<string, MealIntent | null>>({}),
      pending: ref(false),
      updating: ref<string[]>([]),
      upsert: async (_planId: string, _status: MealIntentStatus) => {},
    }
  }

  const { data: plansData, pending: plansPending } = useAsyncData<MealPlan[]>(
    () => `meal-plans-${tenantStore.currentTenantId}-${eventId.value}-${templateId.value}`,
    async () => {
      const res = await $fetch<ApiResponse<MealPlan[]>>(
        `/api/proxy/tenants/${tenantStore.currentTenantId}/events/${eventId.value}/meal-templates/${templateId.value}/plans`,
      )
      return res.entity ?? []
    },
    { watch: [eventId, templateId] },
  )

  const plans = computed(() => plansData.value ?? [])
  const intentMap = ref<Record<string, MealIntent | null>>({})
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
            const res = await $fetch<ApiResponse<MealIntent[]>>(
              `/api/proxy/tenants/${tenantStore.currentTenantId}/events/${eventId.value}/meal-templates/${templateId.value}/plans/${plan.id}/intents?memberIds=${encodeURIComponent(memberId.value!)}`,
            )
            return [plan.id, res.entity?.find(i => i.householdMemberId === memberId.value) ?? null] as [string, MealIntent | null]
          }
          catch {
            return [plan.id, null] as [string, MealIntent | null]
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

  async function upsert(planId: string, status: MealIntentStatus, bringOwnFood = false) {
    if (!memberId.value || !householdId.value) return
    updating.value = [...updating.value, planId]
    try {
      await $fetch(
        `/api/proxy/tenants/${tenantStore.currentTenantId}/events/${eventId.value}/meal-templates/${templateId.value}/plans/${planId}/intents?householdId=${encodeURIComponent(householdId.value)}`,
        {
          method: 'PUT',
          body: { householdMemberId: memberId.value, status, bringOwnFood },
        },
      )
      try {
        const res = await $fetch<ApiResponse<MealIntent[]>>(
          `/api/proxy/tenants/${tenantStore.currentTenantId}/events/${eventId.value}/meal-templates/${templateId.value}/plans/${planId}/intents?memberIds=${encodeURIComponent(memberId.value)}`,
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
  return { plans, intentMap, pending, updating, upsert }
}
