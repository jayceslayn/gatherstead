<script setup lang="ts">
import FullCalendar from '@fullcalendar/vue3'
import dayGridPlugin from '@fullcalendar/daygrid'
import listPlugin from '@fullcalendar/list'
import allLocales from '@fullcalendar/core/locales-all'
import type { EventClickArg } from '@fullcalendar/core'

export interface GsCalendarEvent {
  id: string
  title: string
  start: string
  end?: string
  backgroundColor?: string
  borderColor?: string
}

const props = withDefaults(defineProps<{
  events?: GsCalendarEvent[]
  initialView?: 'dayGridMonth' | 'dayGridWeek' | 'listWeek' | 'listMonth'
  initialDate?: string
  height?: string | number
  compact?: boolean
}>(), {
  events: () => [],
  initialView: 'dayGridMonth',
  height: 'auto',
  compact: false,
})

const emit = defineEmits<{
  eventClick: [arg: EventClickArg]
}>()

const { locale } = useI18n()

const calendarOptions = computed(() => ({
  plugins: [dayGridPlugin, listPlugin],
  initialView: props.initialView,
  initialDate: props.initialDate,
  locales: allLocales,
  locale: locale.value,
  events: props.events,
  headerToolbar: props.compact
    ? undefined
    : {
        left: 'prev,next today',
        center: 'title',
        right: '',
      },
  eventClick: (arg: EventClickArg) => emit('eventClick', arg),
  height: props.height,
  fixedWeekCount: false,
}))
</script>

<template>
  <ClientOnly>
    <div class="gs-calendar">
      <FullCalendar :options="calendarOptions" />
    </div>
    <template #fallback>
      <div
        class="rounded-lg bg-elevated animate-pulse"
        :style="{ height: compact ? '180px' : '380px' }"
      />
    </template>
  </ClientOnly>
</template>

<style>
.gs-calendar {
  --fc-border-color: var(--ui-border);
  --fc-neutral-bg-color: var(--ui-bg-elevated);
  --fc-page-bg-color: var(--ui-bg);
  --fc-today-bg-color: color-mix(in oklab, var(--color-harvest-500) 12%, transparent);
  --fc-button-text-color: var(--ui-text);
  --fc-button-bg-color: var(--ui-bg-elevated);
  --fc-button-border-color: var(--ui-border);
  --fc-button-hover-bg-color: var(--ui-bg-accented);
  --fc-button-hover-border-color: var(--ui-border-accented);
  --fc-button-active-bg-color: var(--color-forest-600);
  --fc-button-active-border-color: var(--color-forest-700);
  --fc-button-active-text-color: #fff;
}
.gs-calendar .fc-toolbar-title {
  color: var(--ui-text-highlighted);
}
.gs-calendar .fc-event {
  --fc-event-bg-color: var(--color-harvest-500, #e8873f);
  --fc-event-border-color: var(--color-harvest-600, #d07535);
  cursor: pointer;
}
.gs-calendar .fc-list-event:hover td {
  cursor: pointer;
}
.gs-calendar .fc-list-empty {
  background: transparent;
}
</style>
