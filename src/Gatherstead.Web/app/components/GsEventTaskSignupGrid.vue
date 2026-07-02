<script setup lang="ts">
import type { DropdownMenuItem } from '@nuxt/ui'
import { useHouseholdMembers } from '~/composables/useHouseholdMembers'
import { useEventTaskSignup, type TaskTemplateLane } from '~/composables/useTaskTemplates'

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
  intentMap,
  pending,
  hasPlans,
  isUpdating,
  volunteerCount,
  toggle,
} = useEventTaskSignup(computed(() => props.eventId), computed(() => props.householdId || null))

const selectedDay = computed(() => props.days[selectedDayIndex.value] ?? '')

// Only the current household's volunteers show; plus a picker to add the rest.
function planVolunteers(planId: string) {
  const byMember = intentMap.value[planId] ?? {}
  return members.value.filter(m => byMember[m.id]?.volunteered)
}

function addableOptions(planId: string) {
  const byMember = intentMap.value[planId] ?? {}
  return members.value.filter(m => !byMember[m.id]?.volunteered)
}

// A stateless "Add volunteer" menu — each not-yet-signed-up member volunteers on select.
function addableItems(planId: string, templateId: string): DropdownMenuItem[][] {
  return [addableOptions(planId).map(m => ({
    label: m.name,
    onSelect: () => toggle(planId, templateId, m.id),
  }))]
}

// Each lane is a single time slot, so the slot label leads the subtitle (with the
// minimum-assignees and notes hints), replacing the per-plan slot label.
function laneSubtitle(lane: TaskTemplateLane): string | undefined {
  const parts: string[] = []
  if (lane.timeSlot) parts.push(t(`event.task.${lane.timeSlot.toLowerCase()}`))
  if (lane.template.minimumAssignees) parts.push(t('event.task.minimumAssignees', { n: lane.template.minimumAssignees }))
  if (lane.template.notes) parts.push(lane.template.notes)
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
      :key="`${lane.template.id}:${lane.timeSlot ?? ''}`"
      :title="lane.template.name"
      :subtitle="laneSubtitle(lane)"
      :hide-when-empty="!(lane.plansByDay[selectedDay] ?? []).length"
    >
      <template #day="{ day }">
        <div v-if="(lane.plansByDay[day] ?? []).length" class="space-y-3">
          <div
            v-for="plan in lane.plansByDay[day]"
            :key="plan.id"
            class="space-y-2"
          >
            <div class="flex items-center justify-end">
              <GsTaskCoverageBadge
                :assignee-count="volunteerCount(plan.id)"
                :minimum-assignees="lane.template.minimumAssignees"
                class="shrink-0"
              />
            </div>

            <!-- Current household volunteers, each removable. -->
            <div v-if="planVolunteers(plan.id).length" class="space-y-1">
              <div
                v-for="member in planVolunteers(plan.id)"
                :key="member.id"
                class="flex items-center justify-between gap-2"
              >
                <span class="text-sm truncate">{{ member.name }}</span>
                <UButton
                  color="neutral"
                  variant="ghost"
                  size="xs"
                  square
                  icon="i-heroicons-x-mark"
                  class="shrink-0"
                  :loading="isUpdating(plan.id, member.id)"
                  :aria-label="t('event.task.removeVolunteer', { name: member.name })"
                  @click="toggle(plan.id, lane.template.id, member.id)"
                />
              </div>
            </div>
            <p v-else class="text-xs text-muted">{{ t('event.task.noVolunteers') }}</p>

            <!-- Click to volunteer: pick a not-yet-signed-up household member. -->
            <UDropdownMenu
              v-if="addableOptions(plan.id).length"
              :items="addableItems(plan.id, lane.template.id)"
              :content="{ align: 'start' }"
            >
              <UButton
                color="neutral"
                variant="soft"
                size="xs"
                icon="i-heroicons-plus"
                :label="t('event.task.addVolunteer')"
                block
              />
            </UDropdownMenu>
          </div>
        </div>
      </template>
    </GsSwimlane>
  </GsSwimlaneGroup>
</template>
