<script setup lang="ts">
import type { EventReportDay } from '~/repositories/types'
import { buildAccommodationLanes, buildMealLanes, buildTaskLanes, reportDayTotals } from '~/composables/useReportView'

type Section = 'meals' | 'tasks' | 'accommodations'

const props = defineProps<{
  days: EventReportDay[]
  section: Section
  expanded: Set<string>
}>()

const emit = defineEmits<{ toggle: [id: string] }>()

// Shared with sibling section tabs so switching sections doesn't reset the mobile pager.
const selectedDayIndex = defineModel<number>('selectedDayIndex', { default: 0 })

const { t } = useI18n()

const dayKeys = computed(() => props.days.map(d => d.day))
const dayTotals = computed(() => reportDayTotals(props.days))
const selectedDay = computed(() => props.days[selectedDayIndex.value]?.day)

const mealLanes = computed(() => buildMealLanes(props.days))
const taskLanes = computed(() => buildTaskLanes(props.days))
const accommodationLanes = computed(() => buildAccommodationLanes(props.days))

const accommodationTypeIcon: Record<string, string> = {
  Bedroom: 'i-heroicons-home',
  Bunk: 'i-heroicons-rectangle-stack',
  RvPad: 'i-heroicons-truck',
  Tent: 'i-heroicons-map',
  Offsite: 'i-heroicons-arrow-top-right-on-square',
}
function laneTypeIcon(lane: { byDay: Record<string, { type: string }> }): string {
  const type = Object.values(lane.byDay)[0]?.type
  return accommodationTypeIcon[type ?? 'Bedroom'] ?? 'i-heroicons-home'
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

    <template v-if="section === 'meals'">
      <GsSwimlane
        v-for="lane in mealLanes"
        :key="lane.key"
        :title="lane.title"
        :subtitle="lane.subtitle ? t(`event.meal.${lane.subtitle.toLowerCase()}`) : undefined"
        :hide-when-empty="!selectedDay || !lane.byDay[selectedDay]"
      >
        <template #day="{ day }">
          <GsReportMealCell
            v-if="lane.byDay[day]"
            :meal="lane.byDay[day]!"
            :expanded="expanded"
            @toggle="emit('toggle', $event)"
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
      >
        <template #day="{ day }">
          <GsReportTaskCell
            v-if="lane.byDay[day]"
            :task="lane.byDay[day]!"
            :expanded="expanded"
            @toggle="emit('toggle', $event)"
          />
        </template>
      </GsSwimlane>
    </template>

    <template v-else>
      <GsSwimlane
        v-for="lane in accommodationLanes"
        :key="lane.key"
        :title="lane.title"
      >
        <template #rule-trailing>
          <UIcon :name="laneTypeIcon(lane)" class="size-5 text-primary" />
        </template>
        <template #day="{ day }">
          <GsReportAccommodationCell
            v-if="lane.byDay[day]"
            :acc="lane.byDay[day]!"
            :day="day"
            :expanded="expanded"
            @toggle="emit('toggle', $event)"
          />
        </template>
      </GsSwimlane>
    </template>
  </GsSwimlaneGroup>
</template>
