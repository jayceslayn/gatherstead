import { useTenantStore } from '~/stores/tenant'
import type {
  MealType,
  MealIntentStatus,
  MealTemplate,
  MealPlan,
  MealIntent,
} from '~/repositories/types'
import {
  MEAL_TYPE_FLAGS,
  ALL_MEAL_TYPES,
  mealTypesFromFlags,
} from '~/repositories/types'
import { DemoLimitError } from '~/repositories/interfaces'
import { useRepositories } from '~/composables/useRepositories'

export type { MealType, MealIntentStatus, MealTemplate, MealPlan, MealIntent }
export { ALL_MEAL_TYPES, mealTypesFromFlags, MEAL_TYPE_FLAGS }

export function useMealTemplates(eventId: Ref<string>) {
  const tenantStore = useTenantStore()
  const { mealPlans: repo } = useRepositories()

  const { data, pending, error } = useAsyncData<MealTemplate[]>(
    () => `meal-templates-${tenantStore.currentTenantId}-${eventId.value}`,
    () => repo.listMealTemplates(tenantStore.currentTenantId!, eventId.value),
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
  const { mealPlans: repo } = useRepositories()
  const { t } = useI18n()

  const { data: plansData, pending: plansPending } = useAsyncData<MealPlan[]>(
    () => `meal-plans-${tenantStore.currentTenantId}-${eventId.value}-${templateId.value}`,
    () => repo.listPlans(tenantStore.currentTenantId!, eventId.value, templateId.value),
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
            const intents = await repo.listIntentsForMember(
              tenantStore.currentTenantId!, eventId.value, templateId.value, plan.id, memberId.value!,
            )
            return [plan.id, intents.find((i: MealIntent) => i.householdMemberId === memberId.value) ?? null] as [string, MealIntent | null]
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
      await repo.upsertIntent(
        tenantStore.currentTenantId!, eventId.value, templateId.value, planId,
        householdId.value, memberId.value, status, bringOwnFood,
      )
      try {
        const intents = await repo.listIntentsForMember(
          tenantStore.currentTenantId!, eventId.value, templateId.value, planId, memberId.value,
        )
        intentMap.value[planId] = intents.find((i: MealIntent) => i.householdMemberId === memberId.value) ?? null
      }
      catch {
        intentMap.value[planId] = null
      }
    }
    catch (e) {
      if (e instanceof DemoLimitError) {
        useToast().add({
          title: t('demo.limitReached.title'),
          description: t('demo.limitReached.description'),
          color: 'warning',
        })
        return
      }
      throw e
    }
    finally {
      updating.value = updating.value.filter(id => id !== planId)
    }
  }

  const pending = computed(() => plansPending.value || intentsPending.value)
  return { plans, intentMap, pending, updating, upsert }
}
