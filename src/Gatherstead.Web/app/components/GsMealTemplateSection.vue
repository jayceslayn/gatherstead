<script setup lang="ts">
import type { MealTemplate, MealIntentStatus } from '~/composables/useMealPlans'
import { useMealPlanSection, mealTypesFromFlags } from '~/composables/useMealPlans'
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

const { plans, intentMap, pending, updating, upsert } = useMealPlanSection(
  eventId,
  templateId,
  memberId,
  householdId,
)

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

function getStatus(planId: string): MealIntentStatus | null {
  return intentMap.value[planId]?.status ?? null
}

function isUpdating(planId: string): boolean {
  return updating.value.includes(planId)
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
        <GsAttendanceToggle
          v-if="cellPlanId(row.id, column.id) && memberId"
          :model-value="getStatus(cellPlanId(row.id, column.id)!)"
          :loading="isUpdating(cellPlanId(row.id, column.id)!)"
          size="xs"
          @update:model-value="upsert(cellPlanId(row.id, column.id)!, $event as MealIntentStatus)"
        />
        <span v-else-if="cellPlanId(row.id, column.id)" class="text-xs text-muted">—</span>
      </template>
    </GsDayIntentGrid>
  </UCard>
</template>
