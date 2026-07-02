<script setup lang="ts">
import type { DropdownMenuItem } from '@nuxt/ui'
import { useHouseholdMembers } from '~/composables/useHouseholdMembers'
import { useEventAttendance } from '~/composables/useEventAttendance'
import { useEventMealData } from '~/composables/useEventMealData'
import type { AttendanceStatus, MealPlan } from '~/repositories/types'

const props = defineProps<{
  eventId: string
  days: string[]
  householdId: string
}>()

// Shared with sibling signup grids so switching tabs preserves the mobile day pager.
const selectedDayIndex = defineModel<number>('selectedDayIndex', { default: 0 })

const { t } = useI18n()
const { formatDay } = useFormatDate()
const { attendance, pending: attendancePending, upsert: upsertDay, bulkUpsert: bulkUpsertDays } = useEventAttendance(computed(() => props.eventId))
const { members } = useHouseholdMembers(computed(() => props.householdId))
const { mealPlansByDay, templateNameById, getAttendance: getMealAttendance, upsert: upsertMeal, bulkUpsert: bulkUpsertMeals } = useEventMealData(computed(() => props.eventId))

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
const mealTypeI18nKey: Record<string, string> = {
  Breakfast: 'event.meal.breakfast',
  Lunch: 'event.meal.lunch',
  Dinner: 'event.meal.dinner',
}

// Primary label is the meal template name; falls back to the time-slot label.
function mealLabel(plan: MealPlan): string {
  return templateNameById.value[plan.mealTemplateId] ?? t(mealTypeI18nKey[plan.mealType] ?? plan.mealType)
}

// Muted secondary hint always shows the time slot.
function mealSlotHint(plan: MealPlan): string {
  return t(mealTypeI18nKey[plan.mealType] ?? plan.mealType)
}

// Sub-rows are already ordered by the shared template scheme in useEventMealData.
function sortedMealPlans(day: string): MealPlan[] {
  return mealPlansByDay.value[day] ?? []
}

function mealsVisible(memberId: string, day: string): boolean {
  const status = statusByMemberColumn.value[memberId]?.[day]
  return !!(sortedMealPlans(day).length && (status === 'Going' || status === 'Maybe'))
}

// Per-day column ··· menu — set every member's attendance for that day at once.
function dayActions(day: string): DropdownMenuItem[][] {
  return [[
    { label: t('status.going'), icon: 'i-heroicons-check', onSelect: () => setColumn(day, 'Going') },
    { label: t('status.maybe'), icon: 'i-heroicons-question-mark-circle', onSelect: () => setColumn(day, 'Maybe') },
    { label: t('status.notGoing'), icon: 'i-heroicons-x-mark', onSelect: () => setColumn(day, 'NotGoing') },
  ]]
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

async function setColumn(day: string, status: AttendanceStatus) {
  if (!props.householdId) return
  // Set every member for this day in one bulk call; when marking NotGoing, cascade the day's
  // meals to NotGoing too (mirrors setDayCell) in a single bulk call rather than per member/meal.
  const dayItems = members.value.map(m => ({ memberId: m.id, day, status }))
  const mealItems = status === 'NotGoing'
    ? members.value.flatMap(m =>
        (mealPlansByDay.value[day] ?? []).map(p => ({ planId: p.id, memberId: m.id, status: 'NotGoing' as AttendanceStatus })),
      )
    : []
  await Promise.all([bulkUpsertDays(dayItems), bulkUpsertMeals(mealItems)])
}

// === Bulk wizard ===
const wizardOpen = ref(false)

// Auto-open once per household session when no attendance exists yet.
const dismissedHouseholds = ref(new Set<string>())

watch(
  [() => props.householdId, attendancePending, () => attendance.value.length, () => members.value.length],
  ([householdId, pending, attendanceLen, memberLen]) => {
    if (!householdId || pending || !memberLen) return
    if (dismissedHouseholds.value.has(householdId)) return
    if (attendanceLen === 0) {
      wizardOpen.value = true
      dismissedHouseholds.value.add(householdId)
    }
  },
  { immediate: true },
)

watch(wizardOpen, (isOpen) => {
  if (!isOpen && props.householdId) {
    dismissedHouseholds.value.add(props.householdId)
  }
})
</script>

<template>
  <!-- Member swimlanes — one rule per member, day attendance + meals beneath. -->
  <div v-if="!householdId" class="py-6 text-center text-sm text-muted">
    {{ t('common.loading') }}
  </div>

  <div v-else-if="!members.length" class="py-6 text-center text-sm text-muted">
    {{ t('member.noMembers') }}
  </div>

  <template v-else>
    <!-- Wizard launch button -->
    <div class="flex justify-end mb-4">
      <UButton
        size="sm"
        variant="outline"
        icon="i-heroicons-calendar-days"
        @click="wizardOpen = true"
      >
        {{ t('event.attendanceGrid.bulkSetAttendance') }}
      </UButton>
    </div>

    <GsSwimlaneGroup v-model:selected-day-index="selectedDayIndex" :days="days">
      <template #day-header="{ day }">
        <div class="flex items-start justify-between gap-1">
          <p class="font-semibold text-sm text-highlighted leading-tight min-w-0">{{ formatDay(day) }}</p>
          <UDropdownMenu :items="dayActions(day)">
            <UButton
              size="xs"
              variant="ghost"
              color="neutral"
              icon="i-heroicons-ellipsis-vertical"
              class="shrink-0 -mr-1"
              :aria-label="t('event.attendanceGrid.columnActions', { day: formatDay(day) })"
            />
          </UDropdownMenu>
        </div>
      </template>

      <template #day-total="{ day }">
        <template v-if="totals[day]">
          <span class="inline-flex items-center gap-0.5">
            <UIcon name="i-heroicons-user-group" class="size-3 shrink-0" />
            {{ t('report.event.attendingCount', { n: totals[day]?.going ?? 0 }) }}
          </span>
          <span v-if="totals[day]?.maybe">{{ t('report.event.maybeCount', { n: totals[day]!.maybe }) }}</span>
        </template>
      </template>

      <GsSwimlane
        v-for="member in members"
        :key="member.id"
        :title="member.name"
      >
        <template #day="{ day }">
          <div class="flex justify-end">
            <GsAttendanceToggle
              :model-value="statusByMemberColumn[member.id]?.[day] ?? null"
              :loading="dayUpdating[cellKey(member.id, day)] ?? false"
              size="xs"
              @update:model-value="setDayCell(member.id, day, $event)"
            />
          </div>

          <div v-if="mealsVisible(member.id, day)" class="mt-2 space-y-1.5 border-t border-default pt-2">
            <div
              v-for="plan in sortedMealPlans(day)"
              :key="plan.id"
              class="flex items-center justify-between gap-1"
            >
              <span class="text-xs text-muted min-w-0 truncate">{{ `${mealLabel(plan)} · ${mealSlotHint(plan)}` }}</span>
              <GsAttendanceToggle
                :model-value="getMealAttendance(plan.id, member.id)?.status ?? null"
                :loading="mealUpdating[cellKey(member.id, plan.id)] ?? false"
                size="xs"
                class="shrink-0"
                @update:model-value="setMealCell(member.id, plan.id, $event)"
              />
            </div>
          </div>
        </template>
      </GsSwimlane>
    </GsSwimlaneGroup>
  </template>

  <!-- Bulk attendance wizard — uses the grid's own composable instances so updates reflect immediately. -->
  <GsAttendanceWizardModal
    v-model:open="wizardOpen"
    :days="days"
    :household-id="householdId"
    :members="members"
    :meal-plans-by-day="mealPlansByDay"
    :bulk-upsert-days="bulkUpsertDays"
    :bulk-upsert-meals="bulkUpsertMeals"
  />
</template>
