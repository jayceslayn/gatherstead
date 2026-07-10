<script setup lang="ts">
import type { EventReportDayAttendee } from '~/repositories/types'

// Read-only list of one household's attendees on one day, always visible. In the
// swimlane grid the lane rule carries the household name; the print day-stack has
// no rule, so it opts into `showTitle`.
defineProps<{
  attendees: EventReportDayAttendee[]
  showTitle?: boolean
}>()
</script>

<template>
  <UCard :ui="{ body: 'p-3 sm:p-3' }" class="print:break-inside-avoid">
    <div class="text-sm space-y-2">
      <p v-if="showTitle" class="font-semibold">{{ attendees[0]?.householdName }}</p>
      <ul class="space-y-0.5">
        <li
          v-for="attendee in attendees"
          :key="attendee.memberId"
          class="flex items-center justify-between gap-2"
        >
          <span class="truncate" :class="attendee.status === 'Maybe' ? 'text-muted' : ''">{{ attendee.name }}</span>
          <GsStatusBadge :status="attendee.status" icon-only class="shrink-0" />
        </li>
      </ul>
    </div>
  </UCard>
</template>
