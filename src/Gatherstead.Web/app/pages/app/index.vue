<script setup lang="ts">
import { useTenantRole } from '~/composables/useTenantRole'

definePageMeta({
  layout: 'default',
})

const { t } = useI18n()
const { isManagerOrAbove } = useTenantRole()
</script>

<template>
  <div>
    <GsPageHeader :title="t('nav.dashboard')" />
    <GsEmptyState
      icon="i-heroicons-calendar-days"
      :title="isManagerOrAbove ? t('dashboard.noEventsYet') : t('dashboard.waitingForEvent')"
      :description="isManagerOrAbove ? t('dashboard.noEventsHint') : undefined"
    >
      <UButton v-if="isManagerOrAbove" to="/app/events/create" icon="i-heroicons-plus">
        {{ t('dashboard.createEvent') }}
      </UButton>
    </GsEmptyState>
  </div>
</template>
