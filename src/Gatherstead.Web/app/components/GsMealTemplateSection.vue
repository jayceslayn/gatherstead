<script setup lang="ts">
import type { MealTemplate } from '~/composables/useMealPlans'
import { useMealPlanSection, mealTypesFromFlags } from '~/composables/useMealPlans'
import { useMealAttendance } from '~/composables/useMealAttendance'
import type { AttendanceStatus } from '~/composables/useMealAttendance'
import { useCurrentMemberStore } from '~/stores/member'

const props = defineProps<{
  template: MealTemplate
  eventId: string
}>()

const { t } = useI18n()
const memberStore = useCurrentMemberStore()

const eventId = computed(() => props.eventId)
const templateId = computed(() => props.template.id)
const memberId = computed(() => memberStore.linkedMemberId)
const householdId = computed(() => memberStore.linkedHouseholdId)

const { plans, intentMap, pending: intentsPending, updating: intentUpdating, upsert: upsertIntent } = useMealPlanSection(
  eventId,
  templateId,
  memberId,
  householdId,
)

const { attendancePending, updating: attendanceUpdating, myAttendance, upsert: upsertAttendance } = useMealAttendance(
  eventId,
  templateId,
  plans,
  memberId,
  householdId,
)

const pending = computed(() => intentsPending.value || attendancePending.value)

const days = computed(() => [...new Set(plans.value.map(p => p.day))].sort())
const templateMealTypes = computed(() => mealTypesFromFlags(props.template.mealTypes))

const planGrid = computed(() => {
  const grid: Record<string, Record<string, string>> = {}
  for (const plan of plans.value) {
    const dayEntry = grid[plan.day] ?? {}
    grid[plan.day] = dayEntry
    dayEntry[plan.mealType] = plan.id
  }
  return grid
})

const dayRows = computed(() =>
  days.value.map(d => ({
    id: d,
    label: new Intl.DateTimeFormat(undefined, { weekday: 'short', month: 'short', day: 'numeric' }).format(
      new Date(d + 'T00:00:00'),
    ),
  })),
)

const mealTypeCols = computed(() =>
  templateMealTypes.value.map(m => ({
    id: m,
    label: t(`event.meal.${m.toLowerCase()}`),
  })),
)

function cellPlanId(dayId: string, mealTypeId: string): string | null {
  return planGrid.value[dayId]?.[mealTypeId] ?? null
}

function isVolunteered(planId: string): boolean {
  return intentMap.value[planId]?.volunteered === true
}

function isIntentUpdating(planId: string): boolean {
  return intentUpdating.value.includes(planId)
}

function isAttendanceUpdating(planId: string): boolean {
  return attendanceUpdating.value.includes(planId)
}

function toggleVolunteer(planId: string) {
  upsertIntent(planId, !isVolunteered(planId))
}
</script>

<template>
  <UCard>
    <template #header>
      <p class="font-semibold">{{ template.name }}</p>
      <p v-if="template.notes" class="text-sm text-muted mt-0.5">{{ template.notes }}</p>
    </template>

    <div v-if="pending" class="py-4 text-center text-sm text-muted">
      {{ t('common.loading') }}
    </div>

    <p v-else-if="!plans.length" class="text-sm text-muted">
      {{ t('event.meal.noPlans') }}
    </p>

    <GsDayIntentGrid
      v-else
      :rows="dayRows"
      :columns="mealTypeCols"
    >
      <template #cell="{ row, column }">
        <div v-if="cellPlanId(row.id, column.id)" class="flex flex-col gap-1.5 items-start">
          <GsAttendanceToggle
            v-if="memberId"
            :model-value="myAttendance(cellPlanId(row.id, column.id)!)?.status ?? null"
            :loading="isAttendanceUpdating(cellPlanId(row.id, column.id)!)"
            size="xs"
            @update:model-value="upsertAttendance(cellPlanId(row.id, column.id)!, $event as AttendanceStatus)"
          />
          <span v-else class="text-xs text-muted">—</span>
          <UButton
            v-if="memberId"
            :color="isVolunteered(cellPlanId(row.id, column.id)!) ? 'success' : 'neutral'"
            :variant="isVolunteered(cellPlanId(row.id, column.id)!) ? 'solid' : 'outline'"
            size="xs"
            :loading="isIntentUpdating(cellPlanId(row.id, column.id)!)"
            @click="toggleVolunteer(cellPlanId(row.id, column.id)!)"
          >
            {{ isVolunteered(cellPlanId(row.id, column.id)!) ? t('event.meal.volunteered') : t('event.meal.volunteer') }}
          </UButton>
        </div>
      </template>
    </GsDayIntentGrid>
  </UCard>
</template>
