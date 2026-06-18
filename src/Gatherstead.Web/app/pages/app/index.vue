<script setup lang="ts">
import { useTenantRole } from '~/composables/useTenantRole'

definePageMeta({
  layout: 'default',
})

const { t } = useI18n()
const { isManagerOrAbove } = useTenantRole()
const { upcomingEvents, pending } = useEvents()

const viewMode = ref<'calendar' | 'list'>('list')

onMounted(() => {
  const saved = localStorage.getItem('gs-events-view')
  if (saved === 'list' || saved === 'calendar') viewMode.value = saved
})

watch(viewMode, v => localStorage.setItem('gs-events-view', v))
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

    <div v-else class="grid grid-cols-1 lg:grid-cols-3 gap-6">
      <!-- My Tasks -->
      <div class="lg:col-span-1">
        <h2 class="text-xs font-semibold text-muted uppercase tracking-wider mb-3">
          {{ t('dashboard.myTasks') }}
        </h2>
        <div class="rounded-lg border border-(--ui-border) bg-elevated p-6 flex flex-col items-center text-center gap-2">
          <UIcon name="i-heroicons-clipboard-document-list" class="size-8 text-muted" />
          <p class="text-sm text-muted">{{ t('dashboard.tasksComingSoon') }}</p>
        </div>
      </div>

      <!-- Upcoming Events -->
      <div class="lg:col-span-2">
        <h2 class="text-xs font-semibold text-muted uppercase tracking-wider mb-3">
          {{ t('dashboard.upcomingEvents') }}
        </h2>
        <GsEventCalendarList
          v-model:view-mode="viewMode"
          :events="upcomingEvents"
          :initial-date="upcomingEvents[0]?.startDate"
        />
      </div>
    </div>
  </div>
</template>
