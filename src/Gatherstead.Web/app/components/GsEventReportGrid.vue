<script setup lang="ts">
import type { EventReportDay } from '~/repositories/types'
import { buildAccommodationLanes, buildAttendanceLanes, buildMealLanes, buildTaskLanes, reportDayTotals } from '~/composables/useReportView'
import type { ReportSection as Section } from '~/composables/useReportView'
import { accommodationTypeIcon } from '~/utils/accommodations'

const props = defineProps<{
  days: EventReportDay[]
  section: Section
  expanded: Set<string>
}>()

const emit = defineEmits<{ toggle: [id: string] }>()

// Expansion is lane-keyed (row-wide) and prefixed with the section so lane keys
// can't collide across sections.
function laneId(section: Section, key: string) {
  return `${section}:${key}`
}
function isExpanded(section: Section, key: string) {
  return props.expanded.has(laneId(section, key))
}

// Shared with sibling section tabs so switching sections doesn't reset the mobile pager.
const selectedDayIndex = defineModel<number>('selectedDayIndex', { default: 0 })

const { t } = useI18n()

const dayKeys = computed(() => props.days.map(d => d.day))
const dayTotals = computed(() => reportDayTotals(props.days))
const selectedDay = computed(() => props.days[selectedDayIndex.value]?.day)

const attendanceLanes = computed(() => buildAttendanceLanes(props.days))
const mealLanes = computed(() => buildMealLanes(props.days))
const taskLanes = computed(() => buildTaskLanes(props.days))
const accommodationLanes = computed(() => buildAccommodationLanes(props.days))

function laneTypeIcon(lane: { byDay: Record<string, { type: string }> }): string {
  return accommodationTypeIcon(Object.values(lane.byDay)[0]?.type ?? 'Bedroom')
}
</script>

<template>
  <GsSwimlaneGroup v-model:selected-day-index="selectedDayIndex" :days="dayKeys">
    <template #day-total="{ day }">
      <template v-if="dayTotals[day]">
        <span class="inline-flex items-center gap-0.5">
          <UIcon name="i-heroicons-user-group" class="size-3 shrink-0" />
          {{ t('report.event.attendingCount', { n: dayTotals[day]?.going ?? 0 }) }}
        </span>
        <span v-if="dayTotals[day]?.maybe">{{ t('report.event.maybeCount', { n: dayTotals[day]!.maybe }) }}</span>
      </template>
    </template>

    <template v-if="section === 'attendance'">
      <GsSwimlane
        v-for="lane in attendanceLanes"
        :key="lane.key"
        :title="lane.title"
        :hide-when-empty="!selectedDay || !lane.byDay[selectedDay]"
        collapsible
        :expanded="isExpanded('attendance', lane.key)"
        @toggle="emit('toggle', laneId('attendance', lane.key))"
      >
        <template #day="{ day }">
          <GsReportAttendanceCell
            v-if="lane.byDay[day]"
            :attendees="lane.byDay[day]!"
            :expanded="isExpanded('attendance', lane.key)"
          />
        </template>
      </GsSwimlane>
    </template>

    <template v-else-if="section === 'meals'">
      <GsSwimlane
        v-for="lane in mealLanes"
        :key="lane.key"
        :title="lane.title"
        :subtitle="lane.subtitle ? t(`event.meal.${lane.subtitle.toLowerCase()}`) : undefined"
        :hide-when-empty="!selectedDay || !lane.byDay[selectedDay]"
        collapsible
        :expanded="isExpanded('meals', lane.key)"
        @toggle="emit('toggle', laneId('meals', lane.key))"
      >
        <template #day="{ day }">
          <GsReportMealCell
            v-if="lane.byDay[day]"
            :meal="lane.byDay[day]!"
            :expanded="isExpanded('meals', lane.key)"
          />
        </template>
      </GsSwimlane>
    </template>

    <template v-else-if="section === 'tasks'">
      <GsSwimlane
        v-for="lane in taskLanes"
        :key="lane.key"
        :title="lane.title"
        :subtitle="lane.subtitle ? t(`event.task.${lane.subtitle.toLowerCase()}`) : undefined"
        :hide-when-empty="!selectedDay || !lane.byDay[selectedDay]"
        collapsible
        :expanded="isExpanded('tasks', lane.key)"
        @toggle="emit('toggle', laneId('tasks', lane.key))"
      >
        <template #day="{ day }">
          <GsReportTaskCell
            v-if="lane.byDay[day]"
            :task="lane.byDay[day]!"
            :expanded="isExpanded('tasks', lane.key)"
          />
        </template>
      </GsSwimlane>
    </template>

    <template v-else>
      <GsSwimlane
        v-for="lane in accommodationLanes"
        :key="lane.key"
        :title="lane.title"
        collapsible
        :expanded="isExpanded('accommodations', lane.key)"
        @toggle="emit('toggle', laneId('accommodations', lane.key))"
      >
        <template #rule-leading>
          <UIcon :name="laneTypeIcon(lane)" class="size-5 text-primary" />
        </template>
        <template #day="{ day }">
          <GsReportAccommodationCell
            v-if="lane.byDay[day]"
            :acc="lane.byDay[day]!"
            :expanded="isExpanded('accommodations', lane.key)"
          />
        </template>
      </GsSwimlane>
    </template>
  </GsSwimlaneGroup>
</template>
