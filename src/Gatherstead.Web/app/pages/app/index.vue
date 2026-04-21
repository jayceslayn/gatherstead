<script setup lang="ts">
import { useTenantRole } from '~/composables/useTenantRole'
import type { EventClickArg } from '@fullcalendar/core'

definePageMeta({
  layout: 'default',
})

const { t } = useI18n()
const { isManagerOrAbove } = useTenantRole()
const { upcomingEvents, pending } = useEvents()

const previewEvents = computed(() => upcomingEvents.value.slice(0, 3))

const calendarEvents = computed(() =>
  upcomingEvents.value.map(e => ({
    id: e.id,
    title: e.name,
    start: e.startDate,
    end: e.endDate,
  })),
)

const firstEventDate = computed(() => upcomingEvents.value[0]?.startDate)

function formatDateRange(start: string, end: string) {
  const s = new Date(start + 'T00:00:00')
  const e = new Date(end + 'T00:00:00')
  if (start === end) {
    return new Intl.DateTimeFormat(undefined, { month: 'short', day: 'numeric', year: 'numeric' }).format(s)
  }
  const fmt = new Intl.DateTimeFormat(undefined, { month: 'short', day: 'numeric' })
  return `${fmt.format(s)} – ${fmt.format(e)}, ${new Intl.DateTimeFormat(undefined, { year: 'numeric' }).format(e)}`
}

function onCalendarEventClick(arg: EventClickArg) {
  navigateTo(`/app/events/${arg.event.id}`)
}
</script>

<template>
  <div>
    <GsPageHeader :title="t('nav.dashboard')">
      <UButton v-if="isManagerOrAbove" to="/app/events/create" icon="i-heroicons-plus" size="sm">
        {{ t('dashboard.createEvent') }}
      </UButton>
    </GsPageHeader>

    <div v-if="pending" class="py-16 text-center">
      <p class="text-muted">{{ t('common.loading') }}</p>
    </div>

    <GsEmptyState
      v-else-if="!upcomingEvents.length"
      icon="i-heroicons-calendar-days"
      :title="isManagerOrAbove ? t('dashboard.noEventsYet') : t('dashboard.waitingForEvent')"
      :description="isManagerOrAbove ? t('dashboard.noEventsHint') : undefined"
    >
      <UButton v-if="isManagerOrAbove" to="/app/events/create" icon="i-heroicons-plus">
        {{ t('dashboard.createEvent') }}
      </UButton>
    </GsEmptyState>

    <div v-else class="grid grid-cols-1 md:grid-cols-3 gap-6">
      <!-- Upcoming Events -->
      <div class="space-y-3">
        <h2 class="text-xs font-semibold text-muted uppercase tracking-wider">
          {{ t('dashboard.upcomingEvents') }}
        </h2>
        <UCard
          v-for="event in previewEvents"
          :key="event.id"
          class="cursor-pointer hover:ring-1 hover:ring-primary transition-all"
          @click="navigateTo(`/app/events/${event.id}`)"
        >
          <p class="font-semibold text-sm truncate">{{ event.name }}</p>
          <p class="text-xs text-muted mt-0.5">{{ formatDateRange(event.startDate, event.endDate) }}</p>
        </UCard>
        <UButton
          v-if="upcomingEvents.length > 3"
          to="/app/events"
          variant="ghost"
          size="sm"
          class="w-full"
          trailing-icon="i-heroicons-arrow-right"
        >
          {{ t('dashboard.viewAll') }}
        </UButton>
      </div>

      <!-- Calendar -->
      <div>
        <h2 class="text-xs font-semibold text-muted uppercase tracking-wider mb-3">
          {{ t('nav.events') }}
        </h2>
        <GsCalendar
          :events="calendarEvents"
          initial-view="listWeek"
          :initial-date="firstEventDate"
          :compact="true"
          @event-click="onCalendarEventClick"
        />
      </div>

      <!-- My Tasks -->
      <div>
        <h2 class="text-xs font-semibold text-muted uppercase tracking-wider mb-3">
          {{ t('dashboard.myTasks') }}
        </h2>
        <div class="rounded-lg border border-(--ui-border) bg-elevated p-6 flex flex-col items-center text-center gap-2">
          <UIcon name="i-heroicons-clipboard-document-list" class="size-8 text-muted" />
          <p class="text-sm text-muted">{{ t('dashboard.tasksComingSoon') }}</p>
        </div>
      </div>
    </div>
  </div>
</template>
