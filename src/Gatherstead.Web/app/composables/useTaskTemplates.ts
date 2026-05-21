import { useTenantStore } from '~/stores/tenant'
import type {
  TaskTemplate,
  TaskPlan,
  TaskIntent,
} from '~/repositories/types'
import { DemoLimitError } from '~/repositories/interfaces'
import { useRepositories } from '~/composables/useRepositories'

export function useTaskTemplateActions(eventId: Ref<string>, refresh: () => Promise<void>) {
  const tenantStore = useTenantStore()
  const { tasks: repo } = useRepositories()
  const toast = useToast()
  const { t } = useI18n()
  const { translateError } = useApiError()
  const updating = ref<string[]>([])

  async function createTemplate(name: string, timeSlots: number, startDate: string | null = null, endDate: string | null = null, minimumAssignees: number | null = null, notes: string | null = null) {
    updating.value.push('new')
    try {
      await repo.createTemplate(tenantStore.currentTenantId!, eventId.value, name, timeSlots, startDate, endDate, minimumAssignees, notes)
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

  async function updateTemplate(templateId: string, name: string, timeSlots: number, startDate: string | null = null, endDate: string | null = null, minimumAssignees: number | null = null, notes: string | null = null) {
    updating.value.push(templateId)
    try {
      await repo.updateTemplate(tenantStore.currentTenantId!, eventId.value, templateId, name, timeSlots, startDate, endDate, minimumAssignees, notes)
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

export function useTaskTemplates(eventId: Ref<string>) {
  const tenantStore = useTenantStore()
  const { tasks: repo } = useRepositories()

  const { data, pending, error } = useAsyncData<TaskTemplate[]>(
    () => `task-templates-${tenantStore.currentTenantId}-${eventId.value}`,
    () => repo.listTaskTemplates(tenantStore.currentTenantId!, eventId.value),
    { watch: [eventId] },
  )

  return { templates: computed(() => data.value ?? []), pending, error }
}

export function useTaskPlanSection(
  eventId: Ref<string>,
  templateId: Ref<string>,
  memberId: Ref<string | null>,
  householdId: Ref<string | null>,
) {
  const tenantStore = useTenantStore()
  const { tasks: repo } = useRepositories()
  const { t } = useI18n()

  const { data: plansData, pending: plansPending } = useAsyncData<TaskPlan[]>(
    () => `task-plans-${tenantStore.currentTenantId}-${eventId.value}-${templateId.value}`,
    () => repo.listPlans(tenantStore.currentTenantId!, eventId.value, templateId.value),
    { watch: [eventId, templateId] },
  )

  const plans = computed(() => plansData.value ?? [])
  const intentMap = ref<Record<string, TaskIntent | null>>({})
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
            return [plan.id, intents.find((i: TaskIntent) => i.householdMemberId === memberId.value) ?? null] as [string, TaskIntent | null]
          }
          catch {
            return [plan.id, null] as [string, TaskIntent | null]
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
      await repo.upsertIntent(
        tenantStore.currentTenantId!, eventId.value, templateId.value, planId,
        householdId.value, memberId.value, newValue,
      )
      try {
        const intents = await repo.listIntentsForMember(
          tenantStore.currentTenantId!, eventId.value, templateId.value, planId, memberId.value,
        )
        intentMap.value[planId] = intents.find((i: TaskIntent) => i.householdMemberId === memberId.value) ?? null
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
  return { plans, intentMap, pending, updating, toggle, deleteIntent }
}
