<script setup lang="ts">
import type { EventReportDay, EventReportDayAttendee } from '~/repositories/types'

type Section = 'attendance' | 'meals' | 'tasks' | 'accommodations'

// Cells render collapsed (headline badges) on screen; their detail is revealed
// by print CSS, so no expansion state is threaded through here.
const props = defineProps<{
  day: EventReportDay
  // The single section to render.
  section?: Section
}>()

const { t } = useI18n()
const { formatDay } = useFormatDate()

const showAttendance = computed(() => props.section === 'attendance')
const showMeals = computed(() => props.section === 'meals')
const showTasks = computed(() => props.section === 'tasks')
const showAccommodations = computed(() => props.section === 'accommodations')

// One cell per household (the day's attendees are already ordered household → member).
const attendeesByHousehold = computed(() => {
  const groups = new Map<string, EventReportDayAttendee[]>()
  for (const attendee of props.day.attendees) {
    const group = groups.get(attendee.householdId)
    if (group) group.push(attendee)
    else groups.set(attendee.householdId, [attendee])
  }
  return [...groups.values()]
})
</script>

<template>
  <section class="flex flex-col print:break-inside-avoid">
    <!-- Headline: date + total attendance. Stays visible while the day's detail scrolls.
         In print the sticky positioning is dropped and the header is kept with its cards. -->
    <header class="sticky top-0 z-10 bg-default border-b border-default pb-2 mb-3 print:static print:break-after-avoid">
      <h3 class="font-semibold text-highlighted">{{ formatDay(day.day) }}</h3>
      <div class="flex items-center gap-3 text-sm text-muted mt-0.5">
        <span class="inline-flex items-center gap-1">
          <UIcon name="i-heroicons-user-group" class="size-4" />
          {{ t('report.event.attendingCount', { n: day.going }) }}
        </span>
        <span v-if="day.maybe">{{ t('report.event.maybeCount', { n: day.maybe }) }}</span>
      </div>
    </header>

    <div class="space-y-5">
      <!-- ── Attendance ────────────────────────────────────────── -->
      <div v-if="showAttendance">
        <p v-if="!day.attendees.length" class="text-sm text-muted">{{ t('report.event.noAttendees') }}</p>
        <div v-else class="space-y-2">
          <GsReportAttendanceCell
            v-for="group in attendeesByHousehold"
            :key="group[0]!.householdId"
            :attendees="group"
            show-title
          />
        </div>
      </div>

      <!-- ── Meals ─────────────────────────────────────────────── -->
      <div v-if="showMeals">
        <p v-if="!day.meals.length" class="text-sm text-muted">{{ t('report.event.noMeals') }}</p>
        <div v-else class="space-y-2">
          <GsReportMealCell
            v-for="meal in day.meals"
            :key="meal.mealPlanId"
            :meal="meal"
          />
        </div>
      </div>

      <!-- ── Tasks ─────────────────────────────────────────────── -->
      <div v-if="showTasks">
        <p v-if="!day.tasks.length" class="text-sm text-muted">{{ t('report.event.noTasks') }}</p>
        <div v-else class="space-y-2">
          <GsReportTaskCell
            v-for="task in day.tasks"
            :key="task.taskPlanId"
            :task="task"
            show-title
          />
        </div>
      </div>

      <!-- ── Accommodations ────────────────────────────────────── -->
      <div v-if="showAccommodations">
        <p v-if="!day.accommodations.length" class="text-sm text-muted">{{ t('report.event.noAccommodations') }}</p>
        <div v-else class="space-y-2">
          <GsReportAccommodationCell
            v-for="acc in day.accommodations"
            :key="acc.accommodationId"
            :acc="acc"
          />
        </div>
      </div>
    </div>
  </section>
</template>
