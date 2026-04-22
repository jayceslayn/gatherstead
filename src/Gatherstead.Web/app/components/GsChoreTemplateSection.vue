<script setup lang="ts">
import type { ChoreTemplate } from '~/composables/useChoreTemplates'
import { useChorePlanSection, choreSlotsFromFlags } from '~/composables/useChoreTemplates'
import { useCurrentMemberStore } from '~/stores/member'

const props = defineProps<{
  template: ChoreTemplate
  eventId: string
}>()

const { t } = useI18n()
const memberStore = useCurrentMemberStore()

const eventId = computed(() => props.eventId)
const templateId = computed(() => props.template.id)
const memberId = computed(() => memberStore.linkedMemberId)
const householdId = computed(() => memberStore.linkedHouseholdId)

const { plans, intentMap, pending, updating, toggle } = useChorePlanSection(
  eventId,
  templateId,
  memberId,
  householdId,
)

const days = computed(() => [...new Set(plans.value.map(p => p.day))].sort())
const templateSlots = computed(() => choreSlotsFromFlags(props.template.timeSlots))

const planGrid = computed(() => {
  const grid: Record<string, Record<string, string>> = {}
  for (const plan of plans.value) {
    if (!plan.timeSlot) continue
    const dayEntry = grid[plan.day] ?? {}
    grid[plan.day] = dayEntry
    dayEntry[plan.timeSlot] = plan.id
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

const slotCols = computed(() =>
  templateSlots.value.map(s => ({
    id: s,
    label: t(`event.chore.${s.toLowerCase()}`),
  })),
)

function cellPlanId(dayId: string, slotId: string): string | null {
  return planGrid.value[dayId]?.[slotId] ?? null
}

function isVolunteered(planId: string): boolean {
  return intentMap.value[planId]?.volunteered === true
}

function isUpdating(planId: string): boolean {
  return updating.value.includes(planId)
}
</script>

<template>
  <UCard>
    <template #header>
      <div class="flex items-center gap-2 flex-wrap">
        <p class="font-semibold">{{ template.name }}</p>
        <span v-if="template.minimumAssignees" class="text-xs text-muted">
          {{ t('event.chore.minimumAssignees', { n: template.minimumAssignees }) }}
        </span>
      </div>
      <p v-if="template.notes" class="text-sm text-muted mt-0.5">{{ template.notes }}</p>
    </template>

    <div v-if="pending" class="py-4 text-center text-sm text-muted">
      {{ t('common.loading') }}
    </div>

    <p v-else-if="!plans.length" class="text-sm text-muted">
      {{ t('event.chore.noPlans') }}
    </p>

    <GsDayIntentGrid
      v-else
      :rows="dayRows"
      :columns="slotCols"
    >
      <template #cell="{ row, column }">
        <UButton
          v-if="cellPlanId(row.id, column.id) && memberId"
          :color="isVolunteered(cellPlanId(row.id, column.id)!) ? 'success' : 'neutral'"
          :variant="isVolunteered(cellPlanId(row.id, column.id)!) ? 'solid' : 'outline'"
          size="xs"
          :loading="isUpdating(cellPlanId(row.id, column.id)!)"
          @click="toggle(cellPlanId(row.id, column.id)!)"
        >
          {{ isVolunteered(cellPlanId(row.id, column.id)!) ? t('event.chore.volunteered') : t('event.chore.volunteer') }}
        </UButton>
        <span v-else-if="cellPlanId(row.id, column.id)" class="text-xs text-muted">—</span>
      </template>
    </GsDayIntentGrid>
  </UCard>
</template>
