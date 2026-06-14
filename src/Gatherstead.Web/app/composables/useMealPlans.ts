import { useTenantStore } from '~/stores/tenant'
import type {
  MealTemplate,
  MealPlan,
  MealIntent,
  AttributeWriteEntry,
} from '~/repositories/types'
import { DemoLimitError } from '~/repositories/interfaces'
import { useRepositories } from '~/composables/useRepositories'

export function useMealTemplateActions(eventId: Ref<string>, refresh: () => Promise<void>) {
  const tenantStore = useTenantStore()
  const { mealPlans: repo } = useRepositories()
  const toast = useToast()
  const { t } = useI18n()
  const { translateError } = useApiError()
  const updating = ref<string[]>([])

  async function createTemplate(name: string, mealTypes: number, startDate: string | null = null, endDate: string | null = null, notes: string | null = null, attributes: AttributeWriteEntry[] = [], createMatchingTaskTemplate = false): Promise<boolean> {
    updating.value.push('new')
    try {
      await repo.createTemplate(tenantStore.currentTenantId!, eventId.value, name, mealTypes, startDate, endDate, notes, attributes, createMatchingTaskTemplate)
      await refresh()
      return true
    }
    catch (e) {
      if (e instanceof DemoLimitError) {
        toast.add({ title: t('demo.limitReached.title'), description: t('demo.limitReached.description'), color: 'warning' })
        return false
      }
      toast.add({ title: translateError(e), color: 'error' })
      return false
    }
    finally {
      updating.value = updating.value.filter(k => k !== 'new')
    }
  }

  async function updateTemplate(templateId: string, name: string, mealTypes: number, startDate: string | null = null, endDate: string | null = null, notes: string | null = null, attributes: AttributeWriteEntry[] = []): Promise<boolean> {
    updating.value.push(templateId)
    try {
      await repo.updateTemplate(tenantStore.currentTenantId!, eventId.value, templateId, name, mealTypes, startDate, endDate, notes, attributes)
      await refresh()
      return true
    }
    catch (e) {
      toast.add({ title: translateError(e), color: 'error' })
      return false
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
      toast.add({ title: translateError(e), color: 'error' })
    }
    finally {
      updating.value = updating.value.filter(k => k !== templateId)
    }
  }

  return { updating, createTemplate, updateTemplate, deleteTemplate }
}

export function useMealPlanActions(eventId: Ref<string>, templateId: Ref<string>, refresh: () => Promise<void>) {
  const tenantStore = useTenantStore()
  const { mealPlans: repo } = useRepositories()
  const toast = useToast()
  const { translateError } = useApiError()
  const updating = ref<string[]>([])

  async function updatePlan(planId: string, notes: string | null, isException: boolean, exceptionReason: string | null): Promise<boolean> {
    updating.value.push(planId)
    try {
      await repo.updatePlan(tenantStore.currentTenantId!, eventId.value, templateId.value, planId, notes, isException, exceptionReason)
      await refresh()
      return true
    }
    catch (e) {
      toast.add({ title: translateError(e), color: 'error' })
      return false
    }
    finally {
      updating.value = updating.value.filter(k => k !== planId)
    }
  }

  async function deletePlan(planId: string) {
    updating.value.push(planId)
    try {
      await repo.deletePlan(tenantStore.currentTenantId!, eventId.value, templateId.value, planId)
      await refresh()
    }
    catch (e) {
      toast.add({ title: translateError(e), color: 'error' })
    }
    finally {
      updating.value = updating.value.filter(k => k !== planId)
    }
  }

  return { updating, updatePlan, deletePlan }
}

export function useMealTemplates(eventId: Ref<string>) {
  const tenantStore = useTenantStore()
  const { mealPlans: repo } = useRepositories()

  const { data, pending, error, refresh } = useAsyncData<MealTemplate[]>(
    () => `meal-templates-${tenantStore.currentTenantId}-${eventId.value}`,
    () => repo.listMealTemplates(tenantStore.currentTenantId!, eventId.value),
    { watch: [eventId] },
  )

  return { templates: computed(() => data.value ?? []), pending, error, refresh }
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
