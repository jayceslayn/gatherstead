import { useTenantStore } from '~/stores/tenant'
import type {
  ChoreTimeSlot,
  ChoreTemplate,
  ChorePlan,
  ChoreIntent,
} from '~/repositories/types'
import {
  CHORE_SLOT_FLAGS,
  ALL_CHORE_SLOTS,
  choreSlotsFromFlags,
} from '~/repositories/types'
import { DemoLimitError } from '~/repositories/interfaces'
import { useRepositories } from '~/composables/useRepositories'

export type { ChoreTimeSlot, ChoreTemplate, ChorePlan, ChoreIntent }
export { ALL_CHORE_SLOTS, choreSlotsFromFlags, CHORE_SLOT_FLAGS }

export function useChoreTemplates(eventId: Ref<string>) {
  const tenantStore = useTenantStore()
  const { chores: repo } = useRepositories()

  const { data, pending, error } = useAsyncData<ChoreTemplate[]>(
    () => `chore-templates-${tenantStore.currentTenantId}-${eventId.value}`,
    () => repo.listChoreTemplates(tenantStore.currentTenantId!, eventId.value),
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
  const { chores: repo } = useRepositories()
  const { t } = useI18n()

  const { data: plansData, pending: plansPending } = useAsyncData<ChorePlan[]>(
    () => `chore-plans-${tenantStore.currentTenantId}-${eventId.value}-${templateId.value}`,
    () => repo.listPlans(tenantStore.currentTenantId!, eventId.value, templateId.value),
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
            const intents = await repo.listIntentsForMember(
              tenantStore.currentTenantId!, eventId.value, templateId.value, plan.id, memberId.value!,
            )
            return [plan.id, intents.find((i: ChoreIntent) => i.householdMemberId === memberId.value) ?? null] as [string, ChoreIntent | null]
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
      await repo.upsertIntent(
        tenantStore.currentTenantId!, eventId.value, templateId.value, planId,
        householdId.value, memberId.value, newValue,
      )
      try {
        const intents = await repo.listIntentsForMember(
          tenantStore.currentTenantId!, eventId.value, templateId.value, planId, memberId.value,
        )
        intentMap.value[planId] = intents.find((i: ChoreIntent) => i.householdMemberId === memberId.value) ?? null
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
  return { plans, intentMap, pending, updating, toggle }
}
