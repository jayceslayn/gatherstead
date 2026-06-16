<script setup lang="ts">
import { swimlaneKey } from '~/composables/useSwimlane'

/**
 * Site-wide "swimlane" container. Renders a sticky day-column header (with
 * per-day totals) above a stack of `GsSwimlane` lanes. On desktop the days lay
 * out as aligned columns inside a bounded scroll box; on mobile a day-pager
 * selects one day and every lane renders only that day beneath its rule.
 *
 * The selected day is a `v-model` so a parent can keep one shared value across
 * sibling groups (e.g. the Attendance and Tasks tabs of an event).
 */
const props = withDefaults(defineProps<{
  days: string[]
  /** CSS track size for each day column; defaults to a min-width that forces horizontal scroll when there are many days. */
  dayColWidth?: string
  /** Desktop scroll-box max height. */
  maxHeightClass?: string
}>(), {
  dayColWidth: 'minmax(15rem, 1fr)',
  maxHeightClass: 'lg:max-h-[34rem]',
})

const selectedDayIndex = defineModel<number>('selectedDayIndex', { default: 0 })

const { t } = useI18n()
const { formatDay } = useFormatDate()

const days = computed(() => props.days)

watch(days, (value) => {
  if (selectedDayIndex.value > value.length - 1) {
    selectedDayIndex.value = Math.max(0, value.length - 1)
  }
})

const selectedDay = computed(() => props.days[selectedDayIndex.value])

const gridStyle = computed(() => ({
  gridTemplateColumns: `repeat(${props.days.length}, ${props.dayColWidth})`,
}))

function prevDay() {
  selectedDayIndex.value = Math.max(0, selectedDayIndex.value - 1)
}
function nextDay() {
  selectedDayIndex.value = Math.min(props.days.length - 1, selectedDayIndex.value + 1)
}

provide(swimlaneKey, { days, gridStyle, selectedDayIndex, selectedDay })
</script>

<template>
  <div
    class="lg:overflow-auto lg:rounded-lg lg:border lg:border-default"
    :class="maxHeightClass"
  >
    <!-- ── Sticky header ──────────────────────────────────────────── -->
    <div class="sticky top-[var(--gs-banner-h,0px)] lg:top-0 z-20 bg-default border-b border-default">

      <!-- Desktop: one cell per day, aligned to the lane columns below. -->
      <div class="hidden lg:grid" :style="gridStyle">
        <div
          v-for="(day, i) in days"
          :key="`header-${day}`"
          class="px-2 py-2 border-default"
          :class="i > 0 ? 'border-l' : ''"
        >
          <slot name="day-header" :day="day" :index="i">
            <p class="font-semibold text-sm text-highlighted leading-tight">{{ formatDay(day) }}</p>
          </slot>
          <div class="mt-0.5">
            <slot name="day-total" :day="day" />
          </div>
        </div>
      </div>

      <!-- Mobile: pager doubles as the sticky day header. -->
      <div class="lg:hidden flex items-center justify-between gap-3 px-1 py-2">
        <UButton
          color="neutral"
          variant="subtle"
          size="sm"
          square
          icon="i-heroicons-chevron-left"
          :disabled="selectedDayIndex === 0"
          :class="selectedDayIndex === 0 ? 'opacity-40' : ''"
          :aria-label="t('report.event.prevDay')"
          @click="prevDay"
        />
        <div class="min-w-0 text-center">
          <p class="font-semibold text-sm text-highlighted truncate">{{ selectedDay ? formatDay(selectedDay) : '' }}</p>
          <div v-if="selectedDay" class="flex items-center justify-center gap-2 text-xs text-muted mt-0.5">
            <slot name="day-total" :day="selectedDay" />
          </div>
        </div>
        <UButton
          color="neutral"
          variant="subtle"
          size="sm"
          square
          icon="i-heroicons-chevron-right"
          :disabled="selectedDayIndex >= days.length - 1"
          :class="selectedDayIndex >= days.length - 1 ? 'opacity-40' : ''"
          :aria-label="t('report.event.nextDay')"
          @click="nextDay"
        />
      </div>
    </div>

    <!-- ── Lanes ──────────────────────────────────────────────────── -->
    <slot />
  </div>
</template>
