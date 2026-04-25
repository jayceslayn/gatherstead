import { useTenantStore } from '~/stores/tenant'
import type {
  MealType,
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

export type { MealType, MealTemplate, MealPlan, MealIntent }
export { ALL_MEAL_TYPES, mealTypesFromFlags, MEAL_TYPE_FLAGS }

export function useMealTemplateActions(eventId: Ref<string>, refresh: () => Promise<void>) {
  const tenantStore = useTenantStore()
  const { mealPlans: repo } = useRepositories()
  const toast = useToast()
  const { t } = useI18n()
  const { translateError } = useApiError()
  const updating = ref<string[]>([])

  async function createTemplate(name: string, mealTypes: number, notes: string | null) {
    updating.value.push('new')
    try {
      await repo.createTemplate(tenantStore.currentTenantId!, eventId.value, name, mealTypes, notes)
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

  async function updateTemplate(templateId: string, name: string, mealTypes: number, notes: string | null) {
    updating.value.push(templateId)
    try {
      await repo.updateTemplate(tenantStore.currentTenantId!, eventId.value, templateId, name, mealTypes, notes)
      await refresh()
    }
    catch (e) {
      toast.add({ title: translateError(e as { code: string }), color: 'error' })
    }
    finally {
      updating.value = updating.value.filter(k => k !== templateId)
    }
  }

  async function deleteTemplate(templateId: string) {
    updating.value.push(templateId)
    try {
      await repo.deleteTemplate(tenantStore.currentTenantId!, eventId.value, templateId)
      await refresh()
    }
    catch (e) {
      toast.add({ title: translateError(e as { code: string }), color: 'error' })
    }
    finally {
      updating.value = updating.value.filter(k => k !== templateId)
    }
  }

  return { updating, createTemplate, updateTemplate, deleteTemplate }
}

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

  async function upsert(planId: string, volunteered: boolean) {
    if (!memberId.value || !householdId.value) return
    updating.value = [...updating.value, planId]
    try {
      await repo.upsertIntent(
        tenantStore.currentTenantId!, eventId.value, templateId.value, planId,
        householdId.value, memberId.value, volunteered,
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

  async function deleteIntent(planId: string, intentId: string) {
    if (!memberId.value) return
    updating.value = [...updating.value, planId]
    try {
      await repo.deleteIntent(tenantStore.currentTenantId!, eventId.value, templateId.value, planId, intentId)
      intentMap.value[planId] = null
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
  return { plans, intentMap, pending, updating, upsert, deleteIntent }
}
