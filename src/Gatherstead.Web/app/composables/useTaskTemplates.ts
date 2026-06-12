import { useTenantStore } from '~/stores/tenant'
import type {
  TaskTemplate,
  TaskPlan,
  TaskIntent,
  AttributeWriteEntry,
} from '~/repositories/types'
import { DemoLimitError } from '~/repositories/interfaces'
import { useRepositories } from '~/composables/useRepositories'

export interface TaskPlanWithTemplate {
  plan: TaskPlan
  template: TaskTemplate
}

export function useTaskTemplateActions(eventId: Ref<string>, refresh: () => Promise<void>) {
  const tenantStore = useTenantStore()
  const { tasks: repo } = useRepositories()
  const toast = useToast()
  const { t } = useI18n()
  const { translateError } = useApiError()
  const updating = ref<string[]>([])

  async function createTemplate(name: string, timeSlots: number, startDate: string | null = null, endDate: string | null = null, minimumAssignees: number | null = null, notes: string | null = null, attributes: AttributeWriteEntry[] = []): Promise<boolean> {
    updating.value.push('new')
    try {
      await repo.createTemplate(tenantStore.currentTenantId!, eventId.value, name, timeSlots, startDate, endDate, minimumAssignees, notes, attributes)
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

  async function updateTemplate(templateId: string, name: string, timeSlots: number, startDate: string | null = null, endDate: string | null = null, minimumAssignees: number | null = null, notes: string | null = null, attributes: AttributeWriteEntry[] = []): Promise<boolean> {
    updating.value.push(templateId)
    try {
      await repo.updateTemplate(tenantStore.currentTenantId!, eventId.value, templateId, name, timeSlots, startDate, endDate, minimumAssignees, notes, attributes)
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

export function useTaskTemplates(eventId: Ref<string>) {
  const tenantStore = useTenantStore()
  const { tasks: repo } = useRepositories()

  const { data, pending, error, refresh } = useAsyncData<TaskTemplate[]>(
    () => `task-templates-${tenantStore.currentTenantId}-${eventId.value}`,
    () => repo.listTaskTemplates(tenantStore.currentTenantId!, eventId.value),
    { watch: [eventId] },
  )

  return { templates: computed(() => data.value ?? []), pending, error, refresh }
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

/**
 * Aggregates every task plan across all of an event's templates and tracks the
 * volunteer state of an arbitrary set of household members — powering the
 * day-column sign-up layout where each plan lists members with a toggle each.
 */
export function useEventTaskSignup(
  eventId: Ref<string>,
  householdId: Ref<string | null>,
) {
  const tenantStore = useTenantStore()
  const { tasks: repo } = useRepositories()
  const { t } = useI18n()
  const toast = useToast()
  const { translateError } = useApiError()

  // Templates + their plans rarely change, so they load independently of intents.
  const { data: itemsData, pending: itemsPending } = useAsyncData<TaskPlanWithTemplate[]>(
    () => `task-signup-${tenantStore.currentTenantId}-${eventId.value}`,
    async () => {
      const tenantId = tenantStore.currentTenantId!
      const templates = await repo.listTaskTemplates(tenantId, eventId.value)
      const planLists = await Promise.all(
        templates.map(tpl => repo.listPlans(tenantId, eventId.value, tpl.id)),
      )
      const result: TaskPlanWithTemplate[] = []
      templates.forEach((tpl, i) => {
        for (const plan of planLists[i] ?? []) result.push({ plan, template: tpl })
      })
      return result
    },
    { watch: [eventId, () => tenantStore.currentTenantId] },
  )

  const items = computed(() => itemsData.value ?? [])

  // [planId][memberId] → intent
  const intentMap = ref<Record<string, Record<string, TaskIntent>>>({})
  const intentsPending = ref(false)
  const updating = ref<string[]>([])

  function indexIntents(planId: string, intents: TaskIntent[]) {
    const byMember: Record<string, TaskIntent> = {}
    for (const intent of intents) byMember[intent.householdMemberId] = intent
    intentMap.value = { ...intentMap.value, [planId]: byMember }
  }

  async function loadIntents() {
    const tenantId = tenantStore.currentTenantId
    if (!tenantId || !items.value.length) {
      intentMap.value = {}
      return
    }
    intentsPending.value = true
    try {
      const lists = await Promise.all(
        items.value.map(it =>
          repo.listPlanIntents(tenantId, eventId.value, it.template.id, it.plan.id).catch(() => [] as TaskIntent[]),
        ),
      )
      const map: Record<string, Record<string, TaskIntent>> = {}
      items.value.forEach((it, i) => {
        const byMember: Record<string, TaskIntent> = {}
        for (const intent of lists[i] ?? []) byMember[intent.householdMemberId] = intent
        map[it.plan.id] = byMember
      })
      intentMap.value = map
    }
    finally {
      intentsPending.value = false
    }
  }

  watch(items, () => loadIntents(), { immediate: true })

  // Plans grouped by day, day keys sorted ascending.
  const plansByDay = computed<Record<string, TaskPlanWithTemplate[]>>(() => {
    const grouped: Record<string, TaskPlanWithTemplate[]> = {}
    for (const item of items.value) {
      (grouped[item.plan.day] ??= []).push(item)
    }
    return grouped
  })

  function updatingKey(planId: string, memberId: string) {
    return `${planId}:${memberId}`
  }

  function isVolunteered(planId: string, memberId: string): boolean {
    return intentMap.value[planId]?.[memberId]?.volunteered === true
  }

  function isUpdating(planId: string, memberId: string): boolean {
    return updating.value.includes(updatingKey(planId, memberId))
  }

  function volunteerCount(planId: string): number {
    const byMember = intentMap.value[planId]
    if (!byMember) return 0
    return Object.values(byMember).filter(i => i.volunteered).length
  }

  async function toggle(planId: string, templateId: string, memberId: string) {
    if (!householdId.value) return
    const tenantId = tenantStore.currentTenantId!
    const newValue = !isVolunteered(planId, memberId)
    const key = updatingKey(planId, memberId)
    updating.value = [...updating.value, key]
    try {
      await repo.upsertIntent(tenantId, eventId.value, templateId, planId, householdId.value, memberId, newValue)
      const intents = await repo.listPlanIntents(tenantId, eventId.value, templateId, planId).catch(() => [] as TaskIntent[])
      indexIntents(planId, intents)
    }
    catch (e) {
      if (e instanceof DemoLimitError) {
        toast.add({ title: t('demo.limitReached.title'), description: t('demo.limitReached.description'), color: 'warning' })
        return
      }
      toast.add({ title: translateError(e), color: 'error' })
    }
    finally {
      updating.value = updating.value.filter(k => k !== key)
    }
  }

  const pending = computed(() => itemsPending.value || intentsPending.value)
  const hasPlans = computed(() => items.value.length > 0)

  return { plansByDay, intentMap, pending, hasPlans, isVolunteered, isUpdating, volunteerCount, toggle }
}
