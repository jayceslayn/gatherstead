<script setup lang="ts">
import { useTenantRole } from '~/composables/useTenantRole'
import type { EventClickArg } from '@fullcalendar/core'

definePageMeta({
  layout: 'default',
})

const { t } = useI18n()
const { isManagerOrAbove } = useTenantRole()
const { events, pending } = useEvents()

const viewMode = ref<'calendar' | 'list'>('calendar')

onMounted(() => {
  const saved = localStorage.getItem('gs-events-view')
  if (saved === 'list' || saved === 'calendar') viewMode.value = saved
})

watch(viewMode, v => localStorage.setItem('gs-events-view', v))

const calendarEvents = computed(() =>
  events.value.map(e => ({
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

function formatDate(date: string) {
  return new Intl.DateTimeFormat(undefined, { year: 'numeric', month: 'short', day: 'numeric' }).format(
    new Date(date + 'T00:00:00'),
  )
}
</script>

<template>
  <div>
    <GsPageHeader :title="t('event.title')">
      <div class="flex items-center gap-2">
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
        <UButton v-if="isManagerOrAbove" to="/app/events/create" icon="i-heroicons-plus" size="sm">
          {{ t('event.createTitle') }}
        </UButton>
      </div>
    </GsPageHeader>

    <div v-if="pending" class="py-16 text-center">
      <p class="text-muted">{{ t('common.loading') }}</p>
    </div>

    <GsEmptyState
      v-else-if="!events.length"
      icon="i-heroicons-calendar-days"
      :title="t('event.noEvents')"
      :description="isManagerOrAbove ? t('event.noEventsHintManager') : t('event.noEventsHintMember')"
    >
      <UButton v-if="isManagerOrAbove" to="/app/events/create" icon="i-heroicons-plus">
        {{ t('event.createTitle') }}
      </UButton>
    </GsEmptyState>

    <template v-else>
      <GsCalendar
        v-if="viewMode === 'calendar'"
        :events="calendarEvents"
        initial-view="dayGridMonth"
        @event-click="onEventClick"
      />

      <div v-else class="space-y-2">
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
                  {{ formatDate(event.startDate) }} – {{ formatDate(event.endDate) }}
                </p>
              </div>
              <UIcon name="i-heroicons-chevron-right" class="size-5 text-muted shrink-0" />
            </div>
          </UCard>
        </NuxtLink>
      </div>
    </template>
  </div>
</template>
