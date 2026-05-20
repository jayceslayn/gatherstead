import { useTenantStore } from '~/stores/tenant'
import type { MealPlan, MealAttendance, AttendanceStatus } from '~/repositories/types'
import { DemoLimitError } from '~/repositories/interfaces'
import { useRepositories } from '~/composables/useRepositories'
import { useMealTemplates } from '~/composables/useMealPlans'

export function useEventMealData(eventId: Ref<string>) {
  const tenantStore = useTenantStore()
  const { mealPlans: plansRepo, mealAttendance: attendanceRepo } = useRepositories()
  const { t } = useI18n()
  const { templates } = useMealTemplates(eventId)

  const allPlans = ref<MealPlan[]>([])
  const attendanceMap = ref<Record<string, MealAttendance[]>>({})
  const plansPending = ref(false)
  const attendancePending = ref(false)

  watch(templates, async (newTemplates) => {
    if (!newTemplates.length) {
      allPlans.value = []
      return
    }
    plansPending.value = true
    try {
      const arrays = await Promise.all(
        newTemplates.map(tmpl =>
          plansRepo.listPlans(tenantStore.currentTenantId!, eventId.value, tmpl.id)
            .catch(() => [] as MealPlan[]),
        ),
      )
      allPlans.value = arrays.flat()
    }
    finally {
      plansPending.value = false
    }
  }, { immediate: true })

  watch(allPlans, async (plans) => {
    if (!plans.length) {
      attendanceMap.value = {}
      return
    }
    attendancePending.value = true
    try {
      const entries = await Promise.all(
        plans.map(async (plan) => {
          try {
            const records = await attendanceRepo.listMealAttendance(
              tenantStore.currentTenantId!, eventId.value, plan.mealTemplateId, plan.id,
            )
            return [plan.id, records] as [string, MealAttendance[]]
          }
          catch {
            return [plan.id, []] as [string, MealAttendance[]]
          }
        }),
      )
      attendanceMap.value = Object.fromEntries(entries)
    }
    finally {
      attendancePending.value = false
    }
  }, { immediate: true })

  const MEAL_ORDER = ['Breakfast', 'Lunch', 'Dinner'] as const

  const mealPlansByDay = computed<Record<string, MealPlan[]>>(() => {
    const result: Record<string, MealPlan[]> = {}
    for (const plan of allPlans.value) {
      if (!result[plan.day]) result[plan.day] = []
      result[plan.day]!.push(plan)
    }
    for (const day of Object.keys(result)) {
      result[day]!.sort(
        (a, b) =>
          MEAL_ORDER.indexOf(a.mealType as typeof MEAL_ORDER[number])
          - MEAL_ORDER.indexOf(b.mealType as typeof MEAL_ORDER[number]),
      )
    }
    return result
  })

  function getAttendance(planId: string, memberId: string): MealAttendance | null {
    return attendanceMap.value[planId]?.find(a => a.householdMemberId === memberId) ?? null
  }

  async function upsert(planId: string, householdId: string, memberId: string, status: AttendanceStatus) {
    const plan = allPlans.value.find(p => p.id === planId)
    if (!plan) return
    try {
      const record = await attendanceRepo.upsertMealAttendance(
        tenantStore.currentTenantId!, eventId.value, plan.mealTemplateId, planId,
        householdId, memberId, status, false,
      )
      const list = [...(attendanceMap.value[planId] ?? [])]
      const idx = list.findIndex(a => a.householdMemberId === memberId)
      if (idx >= 0) list[idx] = record
      else list.push(record)
      attendanceMap.value[planId] = list
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
  }

  const pending = computed(() => plansPending.value || attendancePending.value)

  return { allPlans, mealPlansByDay, pending, getAttendance, upsert }
}
