<script setup lang="ts">
import { useSwimlaneContext } from '~/composables/useSwimlane'

/**
 * One swimlane: a persistent rule carrying a title + optional sub-description
 * for a single entity (member / template), with per-day content rendered below
 * via the `#day` slot. On desktop the rule shows every day even when empty —
 * acting as a "no plan today" artifact. Set `hideWhenEmpty` to hide the lane
 * on mobile when the selected day has no content. Must be a child of
 * `GsSwimlaneGroup`.
 */
defineProps<{
  title: string
  subtitle?: string
  /** When true, hides the entire lane on mobile if the selected day has no content. On desktop the rule always shows. */
  hideWhenEmpty?: boolean
}>()

const { days, gridStyle, selectedDay, selectedDayIndex } = useSwimlaneContext()
</script>

<template>
  <div class="border-t border-default" :class="{ 'hidden lg:block': hideWhenEmpty }">
    <!-- Rule: title + sub-description + trailing slot. -->
    <div class="flex items-center justify-between gap-2 px-2 pt-2 pb-1">
      <div class="min-w-0">
        <p class="font-medium text-sm text-highlighted leading-tight break-words">{{ title }}</p>
        <p v-if="subtitle" class="text-xs text-muted leading-tight">{{ subtitle }}</p>
      </div>
      <div class="shrink-0">
        <slot name="rule-trailing" />
      </div>
    </div>

    <!-- Desktop: per-day cells aligned to the group's columns. -->
    <div class="hidden lg:grid pb-2" :style="gridStyle">
      <div
        v-for="(day, i) in days"
        :key="day"
        class="px-2"
        :class="i > 0 ? 'border-l border-default/60' : ''"
      >
        <slot name="day" :day="day" :index="i" />
      </div>
    </div>

    <!-- Mobile: only the selected day's cell beneath the rule. -->
    <div class="lg:hidden px-2 pb-3">
      <slot v-if="selectedDay" name="day" :day="selectedDay" :index="selectedDayIndex" />
    </div>
  </div>
</template>
