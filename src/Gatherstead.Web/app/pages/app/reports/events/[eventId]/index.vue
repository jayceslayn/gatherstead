<script setup lang="ts">
import { useEventReport } from '~/composables/useEventReport'
import { useTenantRole } from '~/composables/useTenantRole'
import { useFormatDate } from '~/composables/useFormatDate'

definePageMeta({
  layout: 'default',
})

type Section = 'meals' | 'tasks' | 'accommodations'

const { t } = useI18n()
const route = useRoute()
const { isMemberOrAbove } = useTenantRole()

const eventId = computed(() => route.params.eventId as string)
const { report, pending, error } = useEventReport(eventId)
const { formatDate } = useFormatDate()

const days = computed(() => report.value?.days ?? [])

const hasAnyData = computed(() =>
  days.value.some(d => d.going > 0 || d.maybe > 0 || d.meals.length > 0 || d.tasks.length > 0 || d.accommodations.length > 0),
)

// Three independent section tabs, mirroring the event sign-up page. Computed so labels
// re-translate on locale switch.
const tabs = computed(() => [
  { value: 'meals' as Section, label: t('event.meals'), icon: 'i-heroicons-cake' },
  { value: 'tasks' as Section, label: t('event.tasks'), icon: 'i-heroicons-clipboard-document-list' },
  { value: 'accommodations' as Section, label: t('event.accommodations'), icon: 'i-heroicons-home' },
])

const activeTab = ref<Section>('meals')

watch(activeTab, (value) => {
  history.replaceState(null, '', `#${value}`)
})

onMounted(() => {
  const hash = route.hash.substring(1)
  if (tabs.value.some(tab => tab.value === hash)) activeTab.value = hash as Section
})

// Progressive disclosure — attendee/assignee/occupant detail is collapsed by default.
const expanded = ref<Set<string>>(new Set())
function toggle(id: string) {
  const next = new Set(expanded.value)
  if (next.has(id)) next.delete(id)
  else next.add(id)
  expanded.value = next
}

// Mobile single-day pager.
const selectedDayIndex = ref(0)
watch(days, (value) => {
  if (selectedDayIndex.value > value.length - 1) selectedDayIndex.value = Math.max(0, value.length - 1)
})
function prevDay() {
  selectedDayIndex.value = Math.max(0, selectedDayIndex.value - 1)
}
function nextDay() {
  selectedDayIndex.value = Math.min(days.value.length - 1, selectedDayIndex.value + 1)
}
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
      <GsBreadcrumb
        :items="[
          { label: t('report.title'), to: '/app/reports' },
          { label: report.eventName },
        ]"
      />

      <GsPageHeader :title="report.eventName">
        <UButton
          variant="outline"
          size="sm"
          icon="i-heroicons-printer"
          :to="`/app/reports/events/${eventId}/print`"
          target="_blank"
        >
          {{ t('report.event.print') }}
        </UButton>
      </GsPageHeader>

      <div class="flex items-center gap-2 text-sm text-muted mb-6 flex-wrap">
        <UIcon name="i-heroicons-calendar-days" class="size-4 shrink-0" />
        <span>{{ t('event.dateRange', { start: formatDate(report.startDate), end: formatDate(report.endDate) }) }}</span>
      </div>

      <GsEmptyState
        v-if="!hasAnyData"
        icon="i-heroicons-chart-bar"
        :title="t('report.event.noData')"
      />

      <template v-else>
        <!-- Section tabs -->
        <div class="flex border-b border-default mb-4" role="tablist">
          <button
            v-for="tab in tabs"
            :key="tab.value"
            type="button"
            role="tab"
            :aria-selected="activeTab === tab.value"
            class="inline-flex items-center gap-1.5 px-4 py-2.5 text-sm font-medium border-b-2 -mb-px transition-colors"
            :class="activeTab === tab.value
              ? 'border-primary text-primary'
              : 'border-transparent text-muted hover:text-default'"
            @click="activeTab = tab.value"
          >
            <UIcon :name="tab.icon" class="size-4" />
            {{ tab.label }}
          </button>
        </div>

        <!-- Desktop: day columns side by side; headers stick to page scroll. -->
        <div class="hidden lg:flex gap-4 overflow-x-auto pb-2">
          <GsEventReportDay
            v-for="day in days"
            :key="day.day"
            :day="day"
            :section="activeTab"
            :expanded="expanded"
            class="w-80 shrink-0"
            @toggle="toggle"
          />
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
          <GsEventReportDay
            v-if="days[selectedDayIndex]"
            :key="days[selectedDayIndex]!.day"
            :day="days[selectedDayIndex]!"
            :section="activeTab"
            :expanded="expanded"
            @toggle="toggle"
          />
        </div>
      </template>
    </template>

    <GsEmptyState
      v-else
      icon="i-heroicons-exclamation-triangle"
      :title="t('error.notFound')"
    />
  </div>
</template>
