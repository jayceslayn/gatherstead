<script setup lang="ts">
// Lays out per-day sign-up columns left-to-right on desktop and a single-day
// pager on mobile, mirroring the event report. Each day's content is provided
// through the `day` slot so tasks and accommodations can share this scaffold.
const props = defineProps<{ days: string[] }>()

const { t } = useI18n()

const selectedDayIndex = ref(0)
watch(() => props.days, (value) => {
  if (selectedDayIndex.value > value.length - 1) selectedDayIndex.value = Math.max(0, value.length - 1)
})
function prevDay() {
  selectedDayIndex.value = Math.max(0, selectedDayIndex.value - 1)
}
function nextDay() {
  selectedDayIndex.value = Math.min(props.days.length - 1, selectedDayIndex.value + 1)
}
</script>

<template>
  <div>
    <!-- Desktop: day columns side by side; headers stick to page scroll. -->
    <div class="hidden lg:flex gap-4 overflow-x-auto pb-2">
      <div v-for="day in days" :key="day" class="w-80 shrink-0">
        <slot name="day" :day="day" />
      </div>
    </div>

    <!-- Mobile: one day at a time with prev/next navigation. -->
    <div class="lg:hidden">
      <div class="flex items-center justify-between gap-3 mb-3">
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
        <span class="text-sm font-medium text-default">{{ t('report.event.dayOf', { n: selectedDayIndex + 1, total: days.length }) }}</span>
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
      <slot v-if="days[selectedDayIndex]" name="day" :day="days[selectedDayIndex]!" />
    </div>
  </div>
</template>
