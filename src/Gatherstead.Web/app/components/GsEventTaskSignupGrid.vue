<script setup lang="ts">
import { useHouseholdMembers } from '~/composables/useHouseholdMembers'
import { useEventTaskSignup } from '~/composables/useTaskTemplates'
import type { TaskTemplate } from '~/repositories/types'

const props = defineProps<{
  eventId: string
  days: string[]
  householdId: string
  /** Per-day going/maybe counts shown in the sticky header. */
  totalsByDay?: Record<string, { going: number, maybe: number }>
}>()

// Shared with sibling signup grids so switching tabs preserves the mobile day pager.
const selectedDayIndex = defineModel<number>('selectedDayIndex', { default: 0 })

const { t } = useI18n()

const { members } = useHouseholdMembers(computed(() => props.householdId))
const {
  templateLanes,
  pending,
  hasPlans,
  isVolunteered,
  isUpdating,
  volunteerCount,
  toggle,
} = useEventTaskSignup(computed(() => props.eventId), computed(() => props.householdId || null))

const selectedDay = computed(() => props.days[selectedDayIndex.value] ?? '')

function laneSubtitle(template: TaskTemplate): string | undefined {
  const parts: string[] = []
  if (template.minimumAssignees) parts.push(t('event.task.minimumAssignees', { n: template.minimumAssignees }))
  if (template.notes) parts.push(template.notes)
  return parts.length ? parts.join(' · ') : undefined
}
</script>

<template>
  <div v-if="!householdId || pending" class="py-6 text-center text-sm text-muted">
    {{ t('common.loading') }}
  </div>

  <GsEmptyState
    v-else-if="!hasPlans"
    icon="i-heroicons-clipboard-document-list"
    :title="t('event.task.noTemplates')"
  />

  <div v-else-if="!members.length" class="py-6 text-center text-sm text-muted">
    {{ t('member.noMembers') }}
  </div>

  <GsSwimlaneGroup
    v-else
    v-model:selected-day-index="selectedDayIndex"
    :days="days"
    day-col-width="minmax(12rem, 1fr)"
  >
    <template #day-total="{ day }">
      <template v-if="totalsByDay?.[day]">
        <span class="inline-flex items-center gap-0.5">
          <UIcon name="i-heroicons-user-group" class="size-3 shrink-0" />
          {{ t('report.event.attendingCount', { n: totalsByDay[day]?.going ?? 0 }) }}
        </span>
        <span v-if="totalsByDay[day]?.maybe">{{ t('report.event.maybeCount', { n: totalsByDay[day]!.maybe }) }}</span>
      </template>
    </template>

    <GsSwimlane
      v-for="lane in templateLanes"
      :key="lane.template.id"
      :title="lane.template.name"
      :subtitle="laneSubtitle(lane.template)"
      :hide-when-empty="!(lane.plansByDay[selectedDay] ?? []).length"
    >
      <template #day="{ day }">
        <div v-if="(lane.plansByDay[day] ?? []).length" class="space-y-3">
          <div
            v-for="plan in lane.plansByDay[day]"
            :key="plan.id"
            class="space-y-1"
          >
            <div class="flex items-center justify-between gap-1">
              <span v-if="plan.timeSlot" class="text-xs text-dimmed">{{ t(`event.task.${plan.timeSlot.toLowerCase()}`) }}</span>
              <GsTaskCoverageBadge
                :assignee-count="volunteerCount(plan.id)"
                :minimum-assignees="lane.template.minimumAssignees"
                class="shrink-0 ml-auto"
              />
            </div>
            <div class="space-y-1">
              <div
                v-for="member in members"
                :key="member.id"
                class="flex items-center justify-between gap-2"
              >
                <span class="text-sm truncate">{{ member.name }}</span>
                <UButton
                  :color="isVolunteered(plan.id, member.id) ? 'success' : 'neutral'"
                  :variant="isVolunteered(plan.id, member.id) ? 'solid' : 'outline'"
                  size="xs"
                  square
                  icon="i-heroicons-check"
                  class="shrink-0"
                  :loading="isUpdating(plan.id, member.id)"
                  :aria-label="isVolunteered(plan.id, member.id) ? t('event.task.volunteered') : t('event.task.volunteer')"
                  @click="toggle(plan.id, lane.template.id, member.id)"
                />
              </div>
            </div>
          </div>
        </div>
      </template>
    </GsSwimlane>
  </GsSwimlaneGroup>
</template>
