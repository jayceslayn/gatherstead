<script setup lang="ts">
import type { DropdownMenuItem } from '@nuxt/ui'
import { useHouseholdMembers } from '~/composables/useHouseholdMembers'
import { useEventAttendance } from '~/composables/useEventAttendance'
import { useEventMealData } from '~/composables/useEventMealData'
import type { AttendanceStatus, HouseholdMember, MealPlan, MealType } from '~/repositories/types'

const props = defineProps<{
  eventId: string
  days: string[]
  householdId: string
}>()

// Shared with sibling signup grids so switching tabs preserves the mobile day pager.
const selectedDayIndex = defineModel<number>('selectedDayIndex', { default: 0 })

const { t } = useI18n()
const { formatDay } = useFormatDate()
const legacy = useRuntimeConfig().public.legacyAttendanceGrid
const { attendance, upsert: upsertDay } = useEventAttendance(computed(() => props.eventId))
const { members } = useHouseholdMembers(computed(() => props.householdId))
const { mealPlansByDay, templateNameById, getAttendance: getMealAttendance, upsert: upsertMeal } = useEventMealData(computed(() => props.eventId))

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

// True while any of a member's days are mid-update — drives the top quick-response bar spinner.
function memberBulkLoading(memberId: string): boolean {
  return props.days.some(day => dayUpdating.value[cellKey(memberId, day)])
}

// === Meal helpers ===
function mealStatusesForMember(memberId: string, day: string): Record<string, AttendanceStatus | undefined> {
  const result: Record<string, AttendanceStatus | undefined> = {}
  for (const plan of mealPlansByDay.value[day] ?? []) {
    result[plan.id] = getMealAttendance(plan.id, memberId)?.status
  }
  return result
}

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

// Sub-rows ordered by template name, then time slot for stability.
function sortedMealPlans(day: string): MealPlan[] {
  return [...(mealPlansByDay.value[day] ?? [])].sort((a, b) => {
    const nameDiff = mealLabel(a).localeCompare(mealLabel(b))
    if (nameDiff !== 0) return nameDiff
    return MEAL_ORDER.indexOf(a.mealType) - MEAL_ORDER.indexOf(b.mealType)
  })
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
  <!-- Legacy table view, retained behind the `legacyAttendanceGrid` flag for rollback. -->
  <template v-if="legacy">
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
  </template>

  <!-- Card-based layout: swimlane matrix on desktop, day-pager on mobile. -->
  <template v-else>
    <div v-if="!householdId" class="py-6 text-center text-sm text-muted">
      {{ t('common.loading') }}
    </div>

    <div v-else-if="!members.length" class="py-6 text-center text-sm text-muted">
      {{ t('member.noMembers') }}
    </div>

    <template v-else>
      <!-- Collapsible quick-response: set one member across every day at once. -->
      <UCard class="mb-4" :ui="{ body: 'p-3 sm:p-4' }">
        <GsCollapsible button-class="text-sm font-medium">
          {{ t('event.attendanceGrid.respondAllDays') }}
          <template #content>
            <div class="grid gap-x-6 gap-y-2 sm:grid-cols-2 pt-3">
              <div
                v-for="member in members"
                :key="member.id"
                class="flex items-center justify-between gap-2"
              >
                <span class="text-sm min-w-0 truncate">{{ member.name }}</span>
                <GsAttendanceToggle
                  :model-value="null"
                  :loading="memberBulkLoading(member.id)"
                  size="xs"
                  class="shrink-0"
                  @update:model-value="setRow(member.id, $event)"
                />
              </div>
            </div>
          </template>
        </GsCollapsible>
      </UCard>

      <!-- Member swimlanes — one rule per member, day attendance + meals beneath. -->
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
  </template>

  <!-- Bulk-meal follow-up, shared by both the quick-response bar (row) and day header (column). -->
  <GsDayAttendanceBulkModal
    v-model:open="bulkModalOpen"
    :context-line="bulkContextLine"
    :meal-types="bulkMealTypes"
    @apply="applyBulkMeals"
  />
</template>
