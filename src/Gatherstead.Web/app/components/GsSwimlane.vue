<script setup lang="ts">
import { useSwimlaneContext } from '~/composables/useSwimlane'

/**
 * One swimlane: a persistent rule carrying a title + optional sub-description
 * for a single entity (member / template), with per-day content rendered below
 * via the `#day` slot. On desktop the rule shows every day even when empty —
 * acting as a "no plan today" artifact. Set `hideWhenEmpty` to hide the lane
 * on mobile when the selected day has no content. Set `collapsible` to render
 * the rule as a row-wide expand/collapse toggle (chevron at the left edge);
 * the lane's cells decide what a collapsed row shows. Must be a child of
 * `GsSwimlaneGroup`.
 */
defineProps<{
  title: string
  subtitle?: string
  /** When true, hides the entire lane on mobile if the selected day has no content. On desktop the rule always shows. */
  hideWhenEmpty?: boolean
  /** Renders the rule as a click target that toggles the whole row's detail. */
  collapsible?: boolean
  /** Row expansion state, owned by the parent (lane-keyed, not per day). */
  expanded?: boolean
}>()

defineEmits<{ toggle: [] }>()

const { days, gridStyle, selectedDay, selectedDayIndex } = useSwimlaneContext()

const { t } = useI18n()
</script>

<template>
  <!-- The group's shared min-width stretches the lane to the scrollable content width
       (keeping its columns aligned with the header's) so its top separator spans the
       full width when scrolled horizontally. The lane is the y-axis snap target
       (start none — leave x-snapping to the day cells). -->
  <div class="border-t border-default lg:min-w-[var(--gs-swimlane-min-w)] lg:[scroll-snap-align:start_none]" :class="{ 'hidden lg:block': hideWhenEmpty }">
    <!-- Rule: optional collapse chevron + leading slot + title + sub-description + trailing
         slot. When collapsible the whole rule is the toggle, giving a row-wide tap target. -->
    <component
      :is="collapsible ? 'button' : 'div'"
      :type="collapsible ? 'button' : undefined"
      class="w-full flex items-center justify-between gap-2 px-2 pt-2 pb-1 text-left"
      :aria-expanded="collapsible ? expanded : undefined"
      :aria-label="collapsible ? t(expanded ? 'report.event.hideDetails' : 'report.event.showDetails') : undefined"
      @click="collapsible && $emit('toggle')"
    >
      <!-- Pinned to the left edge so the member/template name stays visible while
           scrolling horizontally through days. -->
      <div class="flex items-center gap-2 min-w-0 lg:sticky lg:left-0 z-10 bg-default pr-2">
        <UIcon
          v-if="collapsible"
          name="i-heroicons-chevron-right"
          class="size-4 shrink-0 text-muted transition-transform print:hidden"
          :class="expanded ? 'rotate-90' : ''"
        />
        <span class="shrink-0 empty:hidden">
          <slot name="rule-leading" />
        </span>
        <div class="min-w-0">
          <p class="font-medium text-sm text-highlighted leading-tight break-words">{{ title }}</p>
          <p v-if="subtitle" class="text-xs text-muted leading-tight">{{ subtitle }}</p>
        </div>
      </div>
      <div class="shrink-0">
        <slot name="rule-trailing" />
      </div>
    </component>

    <!-- Desktop: per-day cells aligned to the group's columns. Each cell is an
         x-axis snap target (none start) so horizontal scrolling lands on a column. -->
    <div class="hidden lg:grid pb-2" :style="gridStyle">
      <div
        v-for="(day, i) in days"
        :key="day"
        class="px-2 min-w-0 lg:[scroll-snap-align:none_start]"
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
