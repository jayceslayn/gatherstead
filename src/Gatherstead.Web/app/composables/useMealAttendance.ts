import { useTenantStore } from '~/stores/tenant'
import type { MealAttendance, MealPlan, AttendanceStatus } from '~/repositories/types'
import { DemoLimitError } from '~/repositories/interfaces'
import { useRepositories } from '~/composables/useRepositories'

export type { MealAttendance, AttendanceStatus }

export function useMealAttendance(
  eventId: Ref<string>,
  templateId: Ref<string>,
  plans: Ref<MealPlan[]>,
  memberId: Ref<string | null>,
  householdId: Ref<string | null>,
) {
  const tenantStore = useTenantStore()
  const { mealAttendance: repo } = useRepositories()
  const { t } = useI18n()

  const attendanceMap = ref<Record<string, MealAttendance[]>>({})
  const attendancePending = ref(false)
  const updating = ref<string[]>([])

  async function loadAttendance() {
    if (!plans.value.length) {
      attendanceMap.value = {}
      return
    }
    attendancePending.value = true
    try {
      const entries = await Promise.all(
        plans.value.map(async (plan) => {
          try {
            const records = await repo.listMealAttendance(
              tenantStore.currentTenantId!, eventId.value, templateId.value, plan.id,
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
  }

  watch([plans], () => loadAttendance(), { immediate: true })

  function myAttendance(planId: string): MealAttendance | null {
    return attendanceMap.value[planId]?.find(a => a.householdMemberId === memberId.value) ?? null
  }

  async function upsert(planId: string, status: AttendanceStatus, bringOwnFood = false, notes?: string | null) {
    if (!memberId.value || !householdId.value) return
    updating.value = [...updating.value, planId]
    try {
      const record = await repo.upsertMealAttendance(
        tenantStore.currentTenantId!, eventId.value, templateId.value, planId,
        householdId.value, memberId.value, status, bringOwnFood, notes,
      )
      const list = [...(attendanceMap.value[planId] ?? [])]
      const idx = list.findIndex(a => a.householdMemberId === memberId.value)
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
    finally {
      updating.value = updating.value.filter(id => id !== planId)
    }
  }

  async function deleteAttendance(planId: string, attendanceId: string) {
    if (!memberId.value) return
    updating.value = [...updating.value, planId]
    try {
      await repo.deleteMealAttendance(
        tenantStore.currentTenantId!, eventId.value, templateId.value, planId, attendanceId,
      )
      attendanceMap.value[planId] = (attendanceMap.value[planId] ?? []).filter(a => a.id !== attendanceId)
    }
    finally {
      updating.value = updating.value.filter(id => id !== planId)
    }
  }

  return { attendanceMap, attendancePending, updating, myAttendance, upsert, deleteAttendance }
}
