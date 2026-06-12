<script setup lang="ts">
import { useTemplateFormatting } from '~/composables/useTemplateFormatting'

defineProps<{
  name: string
  timeSlots: number
  startDate: string | null
  endDate: string | null
  minimumAssignees?: number | null
  notes?: string | null
}>()

const { t } = useI18n()
const { formatRange, taskSlotLabels } = useTemplateFormatting()
</script>

<template>
  <UCard>
    <div class="flex items-start justify-between gap-4">
      <div class="min-w-0">
        <div class="flex items-center gap-2 flex-wrap">
          <p class="font-semibold">{{ name }}</p>
          <span v-if="formatRange(startDate, endDate)" class="text-xs text-muted">
            {{ formatRange(startDate, endDate) }}
          </span>
        </div>
        <p class="text-sm text-muted mt-0.5">{{ taskSlotLabels(timeSlots) }}</p>
        <p v-if="minimumAssignees" class="text-xs text-muted mt-0.5">
          {{ t('event.task.minimumAssignees', { n: minimumAssignees }) }}
        </p>
        <p v-if="notes" class="text-sm text-muted mt-1">{{ notes }}</p>
      </div>
      <div class="flex items-center gap-1 shrink-0">
        <slot name="actions" />
      </div>
    </div>
  </UCard>
</template>
