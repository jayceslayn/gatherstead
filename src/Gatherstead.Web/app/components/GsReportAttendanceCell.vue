<script setup lang="ts">
import type { EventReportDayAttendee } from '~/repositories/types'

// One household's attendees on one day. Collapsed shows headline status-count
// badges; expanding the lane (or printing) reveals the member list. In the
// swimlane grid the lane rule carries the household name; the print day-stack
// has no rule, so it opts into `showTitle`.
const props = defineProps<{
  attendees: EventReportDayAttendee[]
  expanded?: boolean
  showTitle?: boolean
}>()

const { t } = useI18n()

const counts = computed(() => ({
  going: props.attendees.filter(a => a.status === 'Going').length,
  maybe: props.attendees.filter(a => a.status === 'Maybe').length,
  notGoing: props.attendees.filter(a => a.status === 'NotGoing').length,
}))
</script>

<template>
  <UCard :ui="{ body: 'p-3 sm:p-3' }" class="print:break-inside-avoid">
    <div class="text-sm space-y-2">
      <p v-if="showTitle" class="font-semibold">{{ attendees[0]?.householdName }}</p>
      <!-- Icon/colour pairs mirror GsAttendanceToggle on the sign-up grid. -->
      <div class="flex flex-wrap gap-1.5">
        <UBadge v-if="counts.going" color="success" variant="subtle" icon="i-heroicons-check">
          {{ t('report.event.goingCount', { n: counts.going }) }}
        </UBadge>
        <UBadge v-if="counts.maybe" color="neutral" variant="subtle" icon="i-heroicons-question-mark-circle">
          {{ t('report.event.maybeCount', { n: counts.maybe }) }}
        </UBadge>
        <UBadge v-if="counts.notGoing" color="error" variant="subtle" icon="i-heroicons-x-mark">
          {{ t('report.event.notGoingCount', { n: counts.notGoing }) }}
        </UBadge>
      </div>
      <!-- Detail stays in the DOM (hidden) so the print variant can reveal it without juggling state. -->
      <ul :class="expanded ? 'space-y-0.5' : 'hidden print:block print:space-y-0.5'">
        <li
          v-for="attendee in attendees"
          :key="attendee.memberId"
          class="flex items-center justify-between gap-2"
        >
          <span class="truncate" :class="attendee.status !== 'Going' ? 'text-muted' : ''">{{ attendee.name }}</span>
          <GsStatusBadge :status="attendee.status" icon-only class="shrink-0" />
        </li>
      </ul>
    </div>
  </UCard>
</template>
