<script setup lang="ts">
import type { TaskPlanWithTemplate } from '~/composables/useTaskTemplates'
import type { HouseholdMember, TaskTimeSlot } from '~/repositories/types'

const props = defineProps<{
  day: string
  plans: TaskPlanWithTemplate[]
  members: HouseholdMember[]
  isVolunteered: (planId: string, memberId: string) => boolean
  isUpdating: (planId: string, memberId: string) => boolean
  volunteerCount: (planId: string) => number
}>()

const emit = defineEmits<{ toggle: [planId: string, templateId: string, memberId: string] }>()

const { t } = useI18n()
const { formatDay } = useFormatDate()

// 'Anytime' leads, then the chronological slots — matching the report ordering.
const SLOT_ORDER: TaskTimeSlot[] = ['Anytime', 'Morning', 'Midday', 'Evening']

// Stable order within a day: by time slot, then template name.
const sortedPlans = computed(() =>
  [...props.plans].sort((a, b) => {
    const slotDiff = SLOT_ORDER.indexOf(a.plan.timeSlot as TaskTimeSlot) - SLOT_ORDER.indexOf(b.plan.timeSlot as TaskTimeSlot)
    if (slotDiff !== 0) return slotDiff
    return a.template.name.localeCompare(b.template.name)
  }),
)
</script>

<template>
  <section class="flex flex-col">
    <header class="sticky top-0 z-10 bg-default border-b border-default pb-2 mb-3">
      <h3 class="font-semibold text-highlighted">{{ formatDay(day) }}</h3>
    </header>

    <p v-if="!sortedPlans.length" class="text-sm text-muted">{{ t('report.event.noTasks') }}</p>

    <div v-else class="space-y-2">
      <UCard
        v-for="{ plan, template } in sortedPlans"
        :key="plan.id"
        :ui="{ body: 'p-3 sm:p-3' }"
      >
        <div class="flex items-start justify-between gap-2">
          <div class="min-w-0">
            <div class="flex items-center gap-2 flex-wrap">
              <p class="font-semibold">{{ template.name }}</p>
              <span v-if="plan.timeSlot" class="text-xs text-muted">{{ t(`event.task.${plan.timeSlot.toLowerCase()}`) }}</span>
            </div>
            <p v-if="template.notes" class="text-xs text-muted mt-0.5">{{ template.notes }}</p>
          </div>
          <GsTaskCoverageBadge
            :assignee-count="volunteerCount(plan.id)"
            :minimum-assignees="template.minimumAssignees"
            class="shrink-0"
          />
        </div>

        <div class="mt-3 space-y-1">
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
              class="shrink-0"
              :loading="isUpdating(plan.id, member.id)"
              @click="emit('toggle', plan.id, template.id, member.id)"
            >
              {{ isVolunteered(plan.id, member.id) ? t('event.task.volunteered') : t('event.task.volunteer') }}
            </UButton>
          </div>
        </div>
      </UCard>
    </div>
  </section>
</template>
