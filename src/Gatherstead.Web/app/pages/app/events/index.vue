<script setup lang="ts">
import { useTenantRole } from '~/composables/useTenantRole'

definePageMeta({
  layout: 'default',
})

const { t } = useI18n()
const { isManagerOrAbove } = useTenantRole()
const { events, pending } = useEvents()

const search = ref('')
const filteredEvents = computed(() => {
  const q = search.value.trim().toLowerCase()
  if (!q) return events.value
  return events.value.filter(e => e.name.toLowerCase().includes(q))
})

const viewMode = ref<'calendar' | 'list'>('list')

onMounted(() => {
  const saved = localStorage.getItem('gs-events-view')
  if (saved === 'list' || saved === 'calendar') viewMode.value = saved
})

watch(viewMode, v => localStorage.setItem('gs-events-view', v))
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

    <div v-if="events.length" class="mb-4">
      <GsSearchInput v-model="search" :placeholder="t('event.searchPlaceholder')" />
    </div>

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

    <GsEmptyState
      v-else-if="!filteredEvents.length"
      icon="i-heroicons-magnifying-glass"
      :title="t('common.noResults')"
    />

    <GsEventCalendarList
      v-else
      v-model:view-mode="viewMode"
      :events="filteredEvents"
      :show-toggle="false"
    />
  </div>
</template>
