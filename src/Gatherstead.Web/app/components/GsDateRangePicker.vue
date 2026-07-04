<script setup lang="ts">
// Shared date-range picker: a popover trigger that opens a range calendar. Exposes ISO
// YYYY-MM-DD strings via two v-models (start-date / end-date) to match the app-wide date
// convention. Optional min/max (ISO) constrain the selectable span (e.g. to an event's days).
import { parseDate, type CalendarDate } from '@internationalized/date'

interface CalendarRange { start: CalendarDate | undefined, end: CalendarDate | undefined }

const props = defineProps<{
  min?: string
  max?: string
  disabled?: boolean
  placeholder?: string
}>()

const startDate = defineModel<string>('startDate', { default: '' })
const endDate = defineModel<string>('endDate', { default: '' })

const { t } = useI18n()
const { formatDateRange } = useFormatDate()

function toCal(iso?: string): CalendarDate | undefined {
  return iso ? parseDate(iso) : undefined
}
function toIso(date: CalendarDate | undefined): string {
  return date ? date.toString() : ''
}

const minValue = computed(() => toCal(props.min))
const maxValue = computed(() => toCal(props.max))

const open = ref(false)

// The range calendar owns this state so it can track its in-progress (single-ended) selection.
// We mirror it to the ISO string models only once both ends are chosen, so parents never see a
// half-selected span. shallowRef avoids Vue's deep UnwrapRef, which would strip the CalendarDate
// class brand (#private) and break assignability to the calendar's DateRange type.
const internalRange = shallowRef<CalendarRange>({ start: toCal(startDate.value), end: toCal(endDate.value) })

watch(internalRange, (r) => {
  if (!r.start || !r.end) return
  const s = r.start.toString()
  const e = r.end.toString()
  if (s !== startDate.value) startDate.value = s
  if (e !== endDate.value) endDate.value = e
  open.value = false
})

// Re-sync when the models change from the outside (form reset, prefill, external edit).
watch([startDate, endDate], ([s, e]) => {
  if (toIso(internalRange.value.start) !== s || toIso(internalRange.value.end) !== e) {
    internalRange.value = { start: toCal(s), end: toCal(e) }
  }
})

const label = computed(() =>
  startDate.value && endDate.value ? formatDateRange(startDate.value, endDate.value) : '')
</script>

<template>
  <UPopover v-model:open="open" :disabled="disabled">
    <UButton
      color="neutral"
      variant="outline"
      icon="i-heroicons-calendar-days"
      block
      :disabled="disabled"
      class="justify-start"
    >
      <span :class="label ? '' : 'text-muted'">
        {{ label || placeholder || t('common.selectDates') }}
      </span>
    </UButton>

    <template #content>
      <UCalendar
        v-model="internalRange"
        range
        :min-value="minValue"
        :max-value="maxValue"
        class="p-2"
      />
    </template>
  </UPopover>
</template>
