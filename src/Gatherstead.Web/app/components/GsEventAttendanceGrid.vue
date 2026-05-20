<script setup lang="ts">
import { useHouseholdMembers } from '~/composables/useHouseholdMembers'
import { useEventAttendance } from '~/composables/useEventAttendance'
import { useEventMealData } from '~/composables/useEventMealData'
import type { AttendanceStatus, HouseholdMember, MealType } from '~/repositories/types'

const props = defineProps<{
  eventId: string
  days: string[]
  householdId: string
}>()

const { t } = useI18n()
const { attendance, upsert: upsertDay } = useEventAttendance(computed(() => props.eventId))
const { members } = useHouseholdMembers(computed(() => props.householdId))
const { mealPlansByDay, getAttendance: getMealAttendance, upsert: upsertMeal } = useEventMealData(computed(() => props.eventId))

// === Columns ===
const columns = computed(() =>
  props.days.map(day => ({
    id: day,
    label: new Intl.DateTimeFormat(undefined, { weekday: 'short', month: 'short', day: 'numeric' }).format(
      new Date(day + 'T00:00:00'),
    ),
  })),
)

// === Day attendance ===
const statusByMemberColumn = computed<Record<string, Record<string, AttendanceStatus | undefined>>>(() => {
  const memberIds = new Set(members.value.map(m => m.id))
  const result: Record<string, Record<string, AttendanceStatus | undefined>> = {}
  for (const m of members.value) result[m.id] = {}
  for (const a of attendance.value) {
    if (memberIds.has(a.householdMemberId)) {
      result[a.householdMemberId]![a.day] = a.status
    }
  }
  return result
})

const totals = computed<Record<string, { going: number, maybe: number }>>(() => {
  const result: Record<string, { going: number, maybe: number }> = {}
  for (const day of props.days) result[day] = { going: 0, maybe: 0 }
  for (const a of attendance.value) {
    const entry = result[a.day]
    if (!entry) continue
    if (a.status === 'Going') entry.going++
    else if (a.status === 'Maybe') entry.maybe++
  }
  return result
})

const dayUpdating = ref<Record<string, boolean>>({})
const mealUpdating = ref<Record<string, boolean>>({})

function cellKey(memberId: string, id: string) {
  return `${memberId}:${id}`
}

// === Meal helpers ===
function mealStatusesForMember(memberId: string, day: string): Record<string, AttendanceStatus | undefined> {
  const result: Record<string, AttendanceStatus | undefined> = {}
  for (const plan of mealPlansByDay.value[day] ?? []) {
    result[plan.id] = getMealAttendance(plan.id, memberId)?.status
  }
  return result
}

// === Cell actions ===
async function setDayCell(memberId: string, day: string, status: AttendanceStatus) {
  if (!props.householdId) return
  const key = cellKey(memberId, day)
  dayUpdating.value[key] = true
  try {
    await upsertDay(props.householdId, memberId, day, status)
    if (status === 'NotGoing') {
      await Promise.all(
        (mealPlansByDay.value[day] ?? []).map(p => setMealCell(memberId, p.id, 'NotGoing')),
      )
    }
  }
  finally {
    dayUpdating.value[key] = false
  }
}

async function setMealCell(memberId: string, planId: string, status: AttendanceStatus) {
  if (!props.householdId) return
  const key = cellKey(memberId, planId)
  mealUpdating.value[key] = true
  try {
    await upsertMeal(planId, props.householdId, memberId, status)
  }
  finally {
    mealUpdating.value[key] = false
  }
}

// === Bulk row / column ===
const bulkModalOpen = ref(false)
const bulkScope = ref<'row' | 'column'>('row')
const bulkMemberId = ref('')
const bulkDay = ref('')

async function setRow(memberId: string, status: AttendanceStatus) {
  await Promise.all(props.days.map(day => setDayCell(memberId, day, status)))
  if (status !== 'NotGoing') {
    bulkScope.value = 'row'
    bulkMemberId.value = memberId
    bulkModalOpen.value = true
  }
}

async function setColumn(day: string, status: AttendanceStatus) {
  await Promise.all(members.value.map(m => setDayCell(m.id, day, status)))
  if (status !== 'NotGoing') {
    bulkScope.value = 'column'
    bulkDay.value = day
    bulkModalOpen.value = true
  }
}

const MEAL_ORDER: MealType[] = ['Breakfast', 'Lunch', 'Dinner']

const bulkMealTypes = computed((): MealType[] => {
  const types = new Set<MealType>()
  const days = bulkScope.value === 'row' ? props.days : [bulkDay.value]
  for (const day of days) {
    for (const plan of mealPlansByDay.value[day] ?? []) types.add(plan.mealType)
  }
  return MEAL_ORDER.filter(mt => types.has(mt))
})

const bulkMember = computed(() => members.value.find(m => m.id === bulkMemberId.value) ?? null)

const bulkDayLabel = computed(() =>
  bulkDay.value
    ? new Intl.DateTimeFormat(undefined, { weekday: 'short', month: 'short', day: 'numeric' }).format(
        new Date(bulkDay.value + 'T00:00:00'),
      )
    : '',
)

const bulkContextLine = computed(() =>
  bulkScope.value === 'row'
    ? t('event.attendanceModal.bulkContextRow', { name: bulkMember.value?.name ?? '' })
    : t('event.attendanceModal.bulkContextColumn', { day: bulkDayLabel.value }),
)

async function applyBulkMeals(assignments: Partial<Record<MealType, AttendanceStatus>>) {
  const targetDays = bulkScope.value === 'row' ? props.days : [bulkDay.value]
  const targetMemberIds = bulkScope.value === 'row' ? [bulkMemberId.value] : members.value.map(m => m.id)
  await Promise.all(
    targetMemberIds.flatMap(memberId =>
      targetDays.flatMap(day =>
        (mealPlansByDay.value[day] ?? []).flatMap((plan) => {
          const status = assignments[plan.mealType]
          return status ? [setMealCell(memberId, plan.id, status)] : []
        }),
      ),
    ),
  )
}

// === Detail modal ===
const detailOpen = ref(false)
const detailMember = ref<HouseholdMember | null>(null)
const detailDay = ref('')

function openDetail(member: HouseholdMember, day: string) {
  detailMember.value = member
  detailDay.value = day
  detailOpen.value = true
}

const detailMealLoading = computed(() => {
  if (!detailMember.value) return {} as Record<string, boolean>
  const result: Record<string, boolean> = {}
  for (const plan of mealPlansByDay.value[detailDay.value] ?? []) {
    result[plan.id] = mealUpdating.value[cellKey(detailMember.value.id, plan.id)] ?? false
  }
  return result
})
</script>

<template>
  <GsAttendanceGrid
    :members="members"
    :columns="columns"
    :status-by-member-column="statusByMemberColumn"
    :totals="totals"
    :updating="dayUpdating"
    :loaded="!!householdId"
    :hint="t('event.attendanceGrid.hintWithModal')"
    @set-row="setRow"
    @set-column="setColumn"
  >
    <template #cell="{ member, column }">
      <GsDayAttendanceCell
        :member="member"
        :day="column.id"
        :day-status="statusByMemberColumn[member.id]?.[column.id] ?? null"
        :day-loading="dayUpdating[cellKey(member.id, column.id)] ?? false"
        :meal-plans="mealPlansByDay[column.id] ?? []"
        :meal-statuses="mealStatusesForMember(member.id, column.id)"
        @open="openDetail(member, column.id)"
      />
    </template>
  </GsAttendanceGrid>

  <GsDayAttendanceModal
    v-if="detailMember && detailDay"
    v-model:open="detailOpen"
    :member="detailMember"
    :day="detailDay"
    :day-status="statusByMemberColumn[detailMember.id]?.[detailDay] ?? null"
    :day-loading="dayUpdating[cellKey(detailMember.id, detailDay)] ?? false"
    :meal-plans="mealPlansByDay[detailDay] ?? []"
    :meal-statuses="mealStatusesForMember(detailMember.id, detailDay)"
    :meal-loading="detailMealLoading"
    @set-day="(status) => detailMember && setDayCell(detailMember.id, detailDay, status)"
    @set-meal="(planId, status) => detailMember && setMealCell(detailMember.id, planId, status)"
  />

  <GsDayAttendanceBulkModal
    v-model:open="bulkModalOpen"
    :context-line="bulkContextLine"
    :meal-types="bulkMealTypes"
    @apply="applyBulkMeals"
  />
</template>
