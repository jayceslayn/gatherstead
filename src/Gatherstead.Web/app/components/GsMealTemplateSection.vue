<script setup lang="ts">
import type { MealTemplate, AttendanceStatus } from '~/repositories/types'
import { useMealPlanSection } from '~/composables/useMealPlans'
import { useMealAttendance } from '~/composables/useMealAttendance'
import { useHouseholdMembers } from '~/composables/useHouseholdMembers'

const props = defineProps<{
  template: MealTemplate
  eventId: string
  householdId: string
}>()

const { t } = useI18n()
const eventId = computed(() => props.eventId)
const templateId = computed(() => props.template.id)

function formatDate(dateStr: string) {
  return new Intl.DateTimeFormat(undefined, { weekday: 'short', month: 'short', day: 'numeric' }).format(
    new Date(dateStr + 'T00:00:00'),
  )
}

const dateRangeLabel = computed(() => {
  const { startDate, endDate } = props.template
  if (!startDate || !endDate) return null
  if (startDate === endDate) return formatDate(startDate)
  return t('event.meal.dateRange', { start: formatDate(startDate), end: formatDate(endDate) })
})

const { plans, pending: plansPending } = useMealPlanSection(
  eventId,
  templateId,
  computed(() => null),
  computed(() => null),
)

const { members, pending: membersPending } = useHouseholdMembers(computed(() => props.householdId))

const { attendancePending, getAttendance, upsert } = useMealAttendance(eventId, templateId, plans)

const pending = computed(() => plansPending.value || membersPending.value || attendancePending.value)

const MEAL_TYPE_ORDER = ['Breakfast', 'Lunch', 'Dinner'] as const

const columns = computed(() =>
  [...plans.value]
    .sort((a, b) => {
      const dayDiff = a.day.localeCompare(b.day)
      if (dayDiff !== 0) return dayDiff
      return MEAL_TYPE_ORDER.indexOf(a.mealType as typeof MEAL_TYPE_ORDER[number]) - MEAL_TYPE_ORDER.indexOf(b.mealType as typeof MEAL_TYPE_ORDER[number])
    })
    .map(plan => ({
      id: plan.id,
      label: new Intl.DateTimeFormat(undefined, { weekday: 'short', month: 'short', day: 'numeric' }).format(
        new Date(plan.day + 'T00:00:00'),
      ),
      sublabel: t(`event.meal.${plan.mealType.toLowerCase()}`),
    })),
)

const statusByMemberColumn = computed<Record<string, Record<string, AttendanceStatus | undefined>>>(() => {
  const result: Record<string, Record<string, AttendanceStatus | undefined>> = {}
  for (const m of members.value) {
    result[m.id] = {}
    for (const plan of plans.value) {
      result[m.id]![plan.id] = getAttendance(plan.id, m.id)?.status
    }
  }
  return result
})

const totals = computed<Record<string, { going: number, maybe: number }>>(() => {
  const result: Record<string, { going: number, maybe: number }> = {}
  for (const plan of plans.value) {
    result[plan.id] = { going: 0, maybe: 0 }
    for (const m of members.value) {
      const status = getAttendance(plan.id, m.id)?.status
      if (status === 'Going') result[plan.id]!.going++
      else if (status === 'Maybe') result[plan.id]!.maybe++
    }
  }
  return result
})

const updating = ref<Record<string, boolean>>({})

function cellKey(memberId: string, planId: string) {
  return `${memberId}:${planId}`
}

async function setCell(memberId: string, planId: string, status: AttendanceStatus) {
  if (!props.householdId) return
  const key = cellKey(memberId, planId)
  updating.value[key] = true
  try {
    await upsert(planId, props.householdId, memberId, status)
  }
  finally {
    updating.value[key] = false
  }
}

async function setRow(memberId: string, status: AttendanceStatus) {
  await Promise.all(plans.value.map(p => setCell(memberId, p.id, status)))
}

async function setColumn(planId: string, status: AttendanceStatus) {
  await Promise.all(members.value.map(m => setCell(m.id, planId, status)))
}
</script>

<template>
  <UCard>
    <template #header>
      <div class="flex items-center gap-2 flex-wrap">
        <p class="font-semibold">{{ template.name }}</p>
        <span v-if="dateRangeLabel" class="text-xs text-muted">{{ dateRangeLabel }}</span>
      </div>
      <p v-if="template.notes" class="text-sm text-muted mt-0.5">{{ template.notes }}</p>
    </template>

    <div v-if="pending" class="py-4 text-center text-sm text-muted">
      {{ t('common.loading') }}
    </div>

    <p v-else-if="!plans.length" class="text-sm text-muted">
      {{ t('event.meal.noPlans') }}
    </p>

    <GsAttendanceGrid
      v-else
      :members="members"
      :columns="columns"
      :status-by-member-column="statusByMemberColumn"
      :totals="totals"
      :updating="updating"
      :loaded="!!householdId"
      @set-cell="setCell"
      @set-row="setRow"
      @set-column="setColumn"
    />
  </UCard>
</template>
