<script setup lang="ts">
import { useEventReport } from '~/composables/useEventReport'
import { useTenantRole } from '~/composables/useTenantRole'
import { useFormatDate } from '~/composables/useFormatDate'
import type { TabsItem } from '@nuxt/ui'
import type { ReportSection as Section } from '~/composables/useReportView'

definePageMeta({
  layout: 'default',
})

const { t } = useI18n()
const route = useRoute()
const { isMemberOrAbove } = useTenantRole()

const eventId = computed(() => route.params.eventId as string)
const { report, pending, error } = useEventReport(eventId)
const { formatDateRange } = useFormatDate()

const days = computed(() => report.value?.days ?? [])

const hasAnyData = computed(() =>
  days.value.some(d => d.going > 0 || d.maybe > 0 || d.meals.length > 0 || d.tasks.length > 0 || d.accommodations.length > 0),
)

// Four independent section tabs, mirroring the event sign-up page (attendance first).
// Computed so labels re-translate on locale switch.
const tabs = computed<TabsItem[]>(() => [
  { value: 'attendance', label: t('event.attendance'), icon: 'i-heroicons-user-group' },
  { value: 'meals', label: t('event.meals'), icon: 'i-heroicons-cake' },
  { value: 'tasks', label: t('event.tasks'), icon: 'i-heroicons-clipboard-document-list' },
  { value: 'accommodations', label: t('event.accommodations'), icon: 'i-heroicons-home' },
])

const activeTab = ref<string | number>('attendance')
const activeSection = computed(() => activeTab.value as Section)

watch(activeTab, (value) => {
  history.replaceState(null, '', `#${value}`)
})

onMounted(() => {
  const hash = route.hash.substring(1)
  if (tabs.value.some(tab => tab.value === hash)) activeTab.value = hash
})

// Progressive disclosure — every row (swimlane) starts collapsed showing only its
// headline badges; keys are `${section}:${laneKey}` so state survives tab switches.
const expanded = ref<Set<string>>(new Set())
function toggle(id: string) {
  const next = new Set(expanded.value)
  if (next.has(id)) next.delete(id)
  else next.add(id)
  expanded.value = next
}

// Shared with sibling section tabs so switching sections doesn't reset the mobile pager.
const selectedDayIndex = ref(0)
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
          icon="i-heroicons-cake"
          :to="`/app/events/${eventId}/meal-planner`"
        >
          {{ t('mealPlanner.open') }}
        </UButton>
        <UButton
          variant="outline"
          size="sm"
          icon="i-heroicons-calendar-days"
          :to="`/app/events/${eventId}`"
        >
          {{ t('report.event.viewSignup') }}
        </UButton>
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
        <span>{{ formatDateRange(report.startDate, report.endDate) }}</span>
      </div>

      <GsEmptyState
        v-if="!hasAnyData"
        icon="i-heroicons-chart-bar"
        :title="t('report.event.noData')"
      />

      <template v-else>
        <!-- Section tabs — same UTabs selector as the event sign-up page. On phones each
             trigger stacks a small label beneath its icon, like the mobile nav bar. -->
        <UTabs
          v-model="activeTab"
          :items="tabs"
          :content="false"
          :ui="{ trigger: 'max-sm:flex-col max-sm:gap-0.5', label: 'max-sm:text-xs' }"
          class="mb-4"
        />

        <GsDismissibleHint
          storage-key="gs-hint-event-report-rows"
          :title="t('common.hint.expandRows.title')"
          :description="t('common.hint.expandRows.body')"
          class="mb-4"
        />

        <GsEventReportGrid
          v-model:selected-day-index="selectedDayIndex"
          :days="days"
          :section="activeSection"
          :expanded="expanded"
          @toggle="toggle"
        />
      </template>
    </template>

    <GsEmptyState
      v-else
      icon="i-heroicons-exclamation-triangle"
      :title="t('error.notFound')"
    />
  </div>
</template>
