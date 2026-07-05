<script setup lang="ts">
import { useTenantRole } from '~/composables/useTenantRole'
import { useFormatDate } from '~/composables/useFormatDate'

definePageMeta({
  layout: 'default',
})

const { t } = useI18n()
const { isMemberOrAbove } = useTenantRole()
const { events, pending } = useEvents()
const { formatDateRange } = useFormatDate()

const search = ref('')
const filteredEvents = computed(() => {
  const q = search.value.trim().toLowerCase()
  if (!q) return events.value
  return events.value.filter(e => e.name.toLowerCase().includes(q))
})
</script>

<template>
  <div>
    <GsPageHeader :title="t('report.title')" />
    <p class="text-muted mb-6">{{ t('report.subtitle') }}</p>

    <GsEmptyState
      v-if="!isMemberOrAbove"
      icon="i-heroicons-lock-closed"
      :title="t('report.noAccess')"
    />

    <template v-else>
      <h2 class="text-sm font-semibold text-muted uppercase tracking-wide mb-3">
        {{ t('report.eventReports') }}
      </h2>
      <p class="text-sm text-muted mb-4">{{ t('report.chooseEvent') }}</p>

      <div v-if="events.length" class="mb-4">
        <GsSearchInput v-model="search" :placeholder="t('report.searchPlaceholder')" />
      </div>

      <div v-if="pending" class="py-16 text-center">
        <p class="text-muted">{{ t('common.loading') }}</p>
      </div>

      <GsEmptyState
        v-else-if="!events.length"
        icon="i-heroicons-chart-bar"
        :title="t('report.noEvents')"
      />

      <GsEmptyState
        v-else-if="!filteredEvents.length"
        icon="i-heroicons-magnifying-glass"
        :title="t('common.noResults')"
      />

      <div v-else class="flex flex-col gap-3">
        <NuxtLink
          v-for="event in filteredEvents"
          :key="event.id"
          :to="`/app/reports/events/${event.id}`"
        >
          <UCard class="hover:ring-1 hover:ring-primary transition-all cursor-pointer">
            <div class="flex items-center justify-between gap-4">
              <div class="min-w-0">
                <p class="font-semibold truncate">{{ event.name }}</p>
                <p class="text-sm text-muted mt-0.5">
                  {{ formatDateRange(event.startDate, event.endDate) }}
                </p>
              </div>
              <UIcon name="i-heroicons-chart-bar" class="size-5 text-muted shrink-0" />
            </div>
          </UCard>
        </NuxtLink>
      </div>
    </template>
  </div>
</template>
