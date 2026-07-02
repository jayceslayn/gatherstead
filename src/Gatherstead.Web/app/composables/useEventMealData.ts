import { useTenantStore } from '~/stores/tenant'
import type { MealPlan, MealAttendance, AttendanceStatus } from '~/repositories/types'
import { DemoLimitError } from '~/repositories/interfaces'
import { useRepositories } from '~/composables/useRepositories'
import { useMealTemplates } from '~/composables/useMealPlans'
import { compareOrderKeys, mealSlotRank, planAggregate } from '~/composables/useTemplateOrder'

export function useEventMealData(eventId: Ref<string>) {
  const tenantStore = useTenantStore()
  const { mealPlans: plansRepo, mealAttendance: attendanceRepo } = useRepositories()
  const { t } = useI18n()
  const { templates } = useMealTemplates(eventId)

  // Plans carry only a mealType (time slot); consumers that want to label a meal
  // by its template name resolve it through this map.
  const templateNameById = computed<Record<string, string>>(() =>
    Object.fromEntries(templates.value.map(tmpl => [tmpl.id, tmpl.name])),
  )

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
      // One event-scoped request instead of one per plan; group the flat list by plan.
      const records = await attendanceRepo
        .listMealAttendanceForEvent(tenantStore.currentTenantId!, eventId.value)
        .catch(() => [] as MealAttendance[])
      const map: Record<string, MealAttendance[]> = {}
      for (const record of records) {
        (map[record.mealPlanId] ??= []).push(record)
      }
      attendanceMap.value = map
    }
    finally {
      attendancePending.value = false
    }
  }, { immediate: true })

  // Per (meal type, template) ordering aggregates — earliest effective plan day and
  // effective plan count — so days order templates consistently with the report.
  const mealOrderByKey = computed<Record<string, { firstEffectiveDay: string, effectiveCount: number }>>(() => {
    const groups: Record<string, MealPlan[]> = {}
    for (const p of allPlans.value) {
      (groups[`${p.mealType}:${p.mealTemplateId}`] ??= []).push(p)
    }
    return Object.fromEntries(Object.entries(groups).map(([k, plans]) => [k, planAggregate(plans)]))
  })

  // Slot order, then the template with the earliest effective plan, then more effective
  // plans, then template title.
  function compareMealPlans(a: MealPlan, b: MealPlan): number {
    const ak = mealOrderByKey.value[`${a.mealType}:${a.mealTemplateId}`]!
    const bk = mealOrderByKey.value[`${b.mealType}:${b.mealTemplateId}`]!
    return compareOrderKeys(
      { slotRank: mealSlotRank(a.mealType), ...ak, title: templateNameById.value[a.mealTemplateId] ?? '' },
      { slotRank: mealSlotRank(b.mealType), ...bk, title: templateNameById.value[b.mealTemplateId] ?? '' },
    )
  }

  const mealPlansByDay = computed<Record<string, MealPlan[]>>(() => {
    const result: Record<string, MealPlan[]> = {}
    for (const plan of allPlans.value) {
      if (!result[plan.day]) result[plan.day] = []
      result[plan.day]!.push(plan)
    }
    for (const day of Object.keys(result)) result[day]!.sort(compareMealPlans)
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

  async function bulkUpsert(items: { planId: string, memberId: string, status: AttendanceStatus }[]) {
    if (!items.length) return
    try {
      const records = await attendanceRepo.bulkUpsertMealAttendance(
        tenantStore.currentTenantId!, eventId.value, items,
      )
      for (const record of records) {
        const list = [...(attendanceMap.value[record.mealPlanId] ?? [])]
        const idx = list.findIndex(a => a.householdMemberId === record.householdMemberId)
        if (idx >= 0) list[idx] = record
        else list.push(record)
        attendanceMap.value[record.mealPlanId] = list
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
  }

  const pending = computed(() => plansPending.value || attendancePending.value)

  return { allPlans, mealPlansByDay, templateNameById, pending, getAttendance, upsert, bulkUpsert }
}
