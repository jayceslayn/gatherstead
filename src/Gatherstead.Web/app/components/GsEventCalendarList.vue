<script setup lang="ts">
import type { EventClickArg } from '@fullcalendar/core'
import type { EventSummary } from '~/repositories/types'

const props = withDefaults(defineProps<{
  events?: EventSummary[]
  showToggle?: boolean
  calendarInitialView?: 'dayGridMonth' | 'listWeek'
  initialDate?: string
}>(), {
  events: () => [],
  showToggle: true,
  calendarInitialView: 'dayGridMonth',
  initialDate: undefined,
})

const viewMode = defineModel<'calendar' | 'list'>('viewMode', { default: 'list' })

const { t } = useI18n()
const { formatDateRange } = useFormatDate()

const calendarEvents = computed(() =>
  props.events.map(e => ({
    id: e.id,
    title: e.name,
    start: e.startDate,
    end: (() => {
      // FullCalendar end is exclusive for all-day events
      const d = new Date(e.endDate + 'T00:00:00')
      d.setDate(d.getDate() + 1)
      return d.toISOString().split('T')[0]
    })(),
  })),
)

function onEventClick(arg: EventClickArg) {
  navigateTo(`/app/events/${arg.event.id}`)
}
</script>

<template>
  <div>
    <div v-if="showToggle" class="flex justify-end mb-3">
      <div class="flex items-center rounded-md border border-(--ui-border) overflow-hidden">
        <UButton
          :color="viewMode === 'calendar' ? 'primary' : 'neutral'"
          :variant="viewMode === 'calendar' ? 'solid' : 'ghost'"
          icon="i-heroicons-calendar-days"
          size="sm"
          :aria-label="t('event.calendarView')"
          class="rounded-none"
          @click="viewMode = 'calendar'"
        />
        <UButton
          :color="viewMode === 'list' ? 'primary' : 'neutral'"
          :variant="viewMode === 'list' ? 'solid' : 'ghost'"
          icon="i-heroicons-list-bullet"
          size="sm"
          :aria-label="t('event.listView')"
          class="rounded-none border-l border-(--ui-border)"
          @click="viewMode = 'list'"
        />
      </div>
    </div>

    <GsCalendar
      v-if="viewMode === 'calendar'"
      :events="calendarEvents"
      :initial-view="calendarInitialView"
      :initial-date="initialDate"
      @event-click="onEventClick"
    />

    <div v-else class="flex flex-col gap-3">
      <NuxtLink
        v-for="event in events"
        :key="event.id"
        :to="`/app/events/${event.id}`"
      >
        <UCard class="hover:ring-1 hover:ring-primary transition-all cursor-pointer">
          <div class="flex items-center justify-between gap-4">
            <div class="min-w-0">
              <p class="font-semibold truncate">{{ event.name }}</p>
              <p class="text-sm text-muted mt-0.5">
                {{ formatDateRange(event.startDate, event.endDate) }}
              </p>
            </div>
            <UIcon name="i-heroicons-chevron-right" class="size-5 text-muted shrink-0" />
          </div>
        </UCard>
      </NuxtLink>
    </div>
  </div>
</template>
