import { useTenantStore } from '~/stores/tenant'
import type { MealAttendance, MealPlan, AttendanceStatus } from '~/repositories/types'
import { DemoLimitError } from '~/repositories/interfaces'
import { useRepositories } from '~/composables/useRepositories'

export type { MealAttendance, AttendanceStatus }

export function useMealAttendance(
  eventId: Ref<string>,
  templateId: Ref<string>,
  plans: Ref<MealPlan[]>,
) {
  const tenantStore = useTenantStore()
  const { mealAttendance: repo } = useRepositories()
  const { t } = useI18n()

  const attendanceMap = ref<Record<string, MealAttendance[]>>({})
  const attendancePending = ref(false)

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

  function getAttendance(planId: string, memberId: string): MealAttendance | null {
    return attendanceMap.value[planId]?.find(a => a.householdMemberId === memberId) ?? null
  }

  async function upsert(planId: string, householdId: string, memberId: string, status: AttendanceStatus, bringOwnFood = false, notes?: string | null) {
    try {
      const record = await repo.upsertMealAttendance(
        tenantStore.currentTenantId!, eventId.value, templateId.value, planId,
        householdId, memberId, status, bringOwnFood, notes,
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

  async function deleteAttendance(planId: string, attendanceId: string) {
    await repo.deleteMealAttendance(
      tenantStore.currentTenantId!, eventId.value, templateId.value, planId, attendanceId,
    )
    attendanceMap.value[planId] = (attendanceMap.value[planId] ?? []).filter(a => a.id !== attendanceId)
  }

  return { attendanceMap, attendancePending, getAttendance, upsert, deleteAttendance }
}
