<script setup lang="ts">
import { useMyStays } from '~/composables/useMyUpcoming'

const props = withDefaults(defineProps<{ limit?: number }>(), { limit: 5 })

const { t } = useI18n()
const { formatDateRange } = useFormatDate()
const { stays, pending } = useMyStays()

const visible = computed(() => stays.value.slice(0, props.limit))
</script>

<template>
  <div>
    <h2 class="text-xs font-semibold text-muted uppercase tracking-wider mb-3">
      {{ t('dashboard.myStays') }}
    </h2>

    <div v-if="pending" class="rounded-lg border border-(--ui-border) bg-elevated p-6 text-center">
      <p class="text-sm text-muted">{{ t('common.loading') }}</p>
    </div>

    <div
      v-else-if="!visible.length"
      class="rounded-lg border border-(--ui-border) bg-elevated p-6 flex flex-col items-center text-center gap-2"
    >
      <UIcon name="i-heroicons-home-modern" class="size-8 text-muted" />
      <p class="text-sm text-muted">{{ t('dashboard.noStays') }}</p>
      <UButton to="/app/accommodations" variant="link" size="xs">{{ t('dashboard.findStay') }}</UButton>
    </div>

    <ul v-else class="space-y-2">
      <li
        v-for="stay in visible"
        :key="stay.id"
        class="rounded-lg border border-(--ui-border) bg-elevated p-3 flex items-center gap-3"
      >
        <UIcon name="i-heroicons-home-modern" class="size-5 text-primary shrink-0" />
        <div class="min-w-0 flex-1">
          <p class="text-sm font-medium truncate">{{ stay.accommodationName }}</p>
          <p class="text-xs text-muted truncate">{{ `${stay.propertyName} · ${formatDateRange(stay.startNight, stay.endNight)}` }}</p>
        </div>
        <GsStatusBadge :status="stay.status" size="xs" />
      </li>
    </ul>
  </div>
</template>
