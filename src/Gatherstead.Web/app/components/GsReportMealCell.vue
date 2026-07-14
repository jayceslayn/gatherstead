<script setup lang="ts">
import type { EventReportMeal } from '~/repositories/types'

// One meal plan on one day. Collapsed shows the headline attendance badges;
// expanding the lane (or printing) reveals dietary needs. The per-attendee list is
// print-only — on screen that detail lives on the Meal Planner page.
const props = defineProps<{
  meal: EventReportMeal
  expanded?: boolean
}>()

const { t } = useI18n()

// Detail stays in the DOM (hidden) so the print variant can reveal it without juggling state.
const detailClass = computed(() =>
  props.expanded ? 'mt-3 space-y-3' : 'hidden print:block print:mt-3 print:space-y-2')
</script>

<template>
  <UCard :ui="{ body: 'p-3 sm:p-3' }" class="print:break-inside-avoid">
    <div class="min-w-0">
      <div class="flex items-center gap-2 flex-wrap">
        <p class="font-semibold">{{ t(`event.meal.${meal.mealType.toLowerCase()}`) }}</p>
        <span class="text-xs text-muted truncate">{{ meal.templateName }}</span>
      </div>
      <!-- Going/Maybe icon/colour pairs mirror GsAttendanceToggle on the sign-up grid. -->
      <div class="flex flex-wrap gap-1.5 mt-2">
        <UBadge color="success" variant="subtle" icon="i-heroicons-check">
          {{ t('report.event.goingCount', { n: meal.going }) }}
        </UBadge>
        <UBadge v-if="meal.maybe" color="neutral" variant="subtle" icon="i-heroicons-question-mark-circle">
          {{ t('report.event.maybeCount', { n: meal.maybe }) }}
        </UBadge>
        <UBadge v-if="meal.bringOwnFood" color="neutral" variant="subtle" icon="i-heroicons-shopping-bag">
          {{ t('report.event.bringingOwnFood', { n: meal.bringOwnFood }) }}
        </UBadge>
        <UBadge v-if="expanded && meal.dietary.length" color="primary" variant="subtle" icon="i-heroicons-heart">
          {{ t('report.event.dietaryCount', { n: meal.dietary.length }) }}
        </UBadge>
      </div>
    </div>

    <div :class="['text-sm', detailClass]">
      <div>
        <p class="text-muted text-xs uppercase tracking-wide mb-1.5">{{ t('report.event.dietaryNeeds') }}</p>
        <p v-if="!meal.dietary.length" class="text-muted">{{ t('report.event.noDietaryNeeds') }}</p>
        <div v-else class="flex flex-wrap gap-1.5">
          <UBadge v-for="d in meal.dietary" :key="d.label" color="primary" variant="subtle">
            {{ t('report.event.dietaryTally', { label: d.label, count: d.count }) }}
          </UBadge>
        </div>
      </div>

      <div v-if="meal.attendees.length" class="hidden print:block">
        <p class="text-muted text-xs uppercase tracking-wide mb-1.5">{{ t('report.event.attendees') }}</p>
        <ul class="space-y-0.5">
          <li v-for="att in meal.attendees" :key="att.memberId" class="flex flex-col gap-0.5">
            <div class="flex items-center justify-between gap-2">
              <span :class="att.status === 'Maybe' ? 'text-muted' : ''">{{ att.name }}</span>
              <span class="flex items-center gap-1.5">
                <GsStatusBadge :status="att.status" icon-only />
                <UBadge v-if="att.bringOwnFood" color="neutral" variant="subtle" icon="i-heroicons-shopping-bag">
                  {{ t('report.event.ownFood') }}
                </UBadge>
              </span>
            </div>
            <p v-if="att.dietaryNotes" class="text-xs text-muted italic pl-0.5">{{ att.dietaryNotes }}</p>
          </li>
        </ul>
      </div>
    </div>
  </UCard>
</template>
