<script setup lang="ts">
import type { AttributeEntry } from '~/repositories/types'
import { useTemplateFormatting } from '~/composables/useTemplateFormatting'

defineProps<{
  name: string
  mealTypes: number
  startDate: string | null
  endDate: string | null
  notes?: string | null
  attributes?: AttributeEntry[]
}>()

const { formatRange, mealTypeLabels } = useTemplateFormatting()
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
        <p class="text-sm text-muted mt-0.5">{{ mealTypeLabels(mealTypes) }}</p>
        <p v-if="notes" class="text-sm text-muted mt-1 break-words whitespace-pre-wrap">{{ notes }}</p>
        <div v-if="attributes?.length" class="mt-2">
          <GsAttributeList :attributes="attributes" />
        </div>
      </div>
      <div class="flex items-center gap-1 shrink-0">
        <slot name="actions" />
      </div>
    </div>
  </UCard>
</template>
