<script setup lang="ts">
import { useEventReport } from '~/composables/useEventReport'
import { useTenantRole } from '~/composables/useTenantRole'
import { useFormatDate } from '~/composables/useFormatDate'

// Dedicated, chrome-free print view. Opened in its own tab from the report page. The document
// is organised by category (Attendance / Meals / Tasks / Accommodations), each starting on a
// fresh page, with the days listed underneath. A CSS running header (see @page block below)
// repeats the event name, generated date, and current category across every printed page. The
// print dialog opens automatically once data is ready.
definePageMeta({
  layout: 'report-print',
})

type Section = 'attendance' | 'meals' | 'tasks' | 'accommodations'

const { t } = useI18n()
const route = useRoute()
const { isMemberOrAbove } = useTenantRole()

const eventId = computed(() => route.params.eventId as string)
const { report, pending, error } = useEventReport(eventId)
const { formatDateRange } = useFormatDate()

const days = computed(() => report.value?.days ?? [])

// One section per category, carrying only the days that actually have data for it. Empty
// categories are dropped so we never print a page of "No meals planned".
const categories = computed(() => {
  const defs: Array<{ value: Section; label: string; key: 'attendees' | 'meals' | 'tasks' | 'accommodations' }> = [
    { value: 'attendance', label: t('event.attendance'), key: 'attendees' },
    { value: 'meals', label: t('event.meals'), key: 'meals' },
    { value: 'tasks', label: t('event.tasks'), key: 'tasks' },
    { value: 'accommodations', label: t('event.accommodations'), key: 'accommodations' },
  ]
  return defs
    .map(def => ({ ...def, days: days.value.filter(d => d[def.key].length > 0) }))
    .filter(c => c.days.length > 0)
})

const hasAnyData = computed(() => categories.value.length > 0)

function printNow() {
  if (import.meta.client) window.print()
}

// Auto-open the print dialog once, after the report has rendered. If the user cancels, the
// on-screen "Print report" button lets them retry.
const hasAutoPrinted = ref(false)
watch(
  [pending, report],
  async () => {
    if (hasAutoPrinted.value || pending.value || !report.value || !hasAnyData.value) return
    if (!isMemberOrAbove.value) return
    hasAutoPrinted.value = true
    await nextTick()
    printNow()
  },
  { immediate: true },
)
</script>

<template>
  <div>
    <GsEmptyState
      v-if="!isMemberOrAbove"
      icon="i-heroicons-lock-closed"
      :title="t('report.noAccess')"
    />

    <div v-else-if="pending" class="py-16 text-center">
      <p class="text-muted">{{ t('common.loading') }}</p>
    </div>

    <GsEmptyState
      v-else-if="error"
      icon="i-heroicons-exclamation-triangle"
      :title="t('error.fetchFailed')"
    />

    <template v-else-if="report">
      <!-- Screen-only toolbar: not part of the printed sheet. -->
      <div class="flex items-center justify-between gap-3 mb-6 print:hidden">
        <UButton
          variant="ghost"
          size="sm"
          icon="i-heroicons-arrow-left"
          :to="`/app/reports/events/${eventId}`"
        >
          {{ t('report.event.backToReport') }}
        </UButton>
        <UButton
          color="primary"
          size="sm"
          icon="i-heroicons-printer"
          @click="printNow"
        >
          {{ t('report.event.printNow') }}
        </UButton>
      </div>

      <GsEmptyState
        v-if="!hasAnyData"
        icon="i-heroicons-chart-bar"
        :title="t('report.event.noData')"
      />

      <template v-else>
        <!-- One table per category. Browsers reprint a <thead> at the top of every page the
             table spans, so it doubles as a running header (event name · dates · category)
             that repeats across pages — unlike @page margin boxes, which Chromium ignores. -->
        <table
          v-for="(category, index) in categories"
          :key="category.value"
          class="w-full border-collapse mb-10 last:mb-0"
          :class="{ 'print:break-before-page': index > 0 }"
        >
          <thead>
            <tr>
              <th class="text-left p-0">
                <div class="flex items-baseline justify-between gap-x-4 gap-y-0.5 flex-wrap bg-default pt-3 pb-1.5 mb-4 border-b-2 border-primary">
                  <div class="flex items-baseline gap-x-3 gap-y-0.5 flex-wrap min-w-0">
                    <span class="text-sm font-semibold text-highlighted truncate">{{ report.eventName }}</span>
                    <span class="text-xs text-muted">{{ formatDateRange(report.startDate, report.endDate) }}</span>
                  </div>
                  <span class="text-sm font-bold uppercase tracking-wide text-primary">{{ category.label }}</span>
                </div>
              </th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="day in category.days" :key="`${category.value}-${day.day}`">
              <td class="align-top pb-8">
                <GsEventReportDay
                  :day="day"
                  :section="category.value"
                />
              </td>
            </tr>
          </tbody>
        </table>
      </template>
    </template>

    <GsEmptyState
      v-else
      icon="i-heroicons-exclamation-triangle"
      :title="t('error.notFound')"
    />
  </div>
</template>
