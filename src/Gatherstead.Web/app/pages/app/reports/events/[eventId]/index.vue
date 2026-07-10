<script setup lang="ts">
import { useEventReport } from '~/composables/useEventReport'
import { useTenantRole } from '~/composables/useTenantRole'
import { useFormatDate } from '~/composables/useFormatDate'

definePageMeta({
  layout: 'default',
})

type Section = 'attendance' | 'meals' | 'tasks' | 'accommodations'

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
const tabs = computed(() => [
  { value: 'attendance' as Section, label: t('event.attendance'), icon: 'i-heroicons-user-group' },
  { value: 'meals' as Section, label: t('event.meals'), icon: 'i-heroicons-cake' },
  { value: 'tasks' as Section, label: t('event.tasks'), icon: 'i-heroicons-clipboard-document-list' },
  { value: 'accommodations' as Section, label: t('event.accommodations'), icon: 'i-heroicons-home' },
])

const activeTab = ref<Section>('attendance')

watch(activeTab, (value) => {
  history.replaceState(null, '', `#${value}`)
})

onMounted(() => {
  const hash = route.hash.substring(1)
  if (tabs.value.some(tab => tab.value === hash)) activeTab.value = hash as Section
})

// Progressive disclosure — meal attendee and accommodation occupant detail is collapsed
// by default (attendance and task cells render inline with nothing to expand).
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

        <GsEventReportGrid
          v-model:selected-day-index="selectedDayIndex"
          :days="days"
          :section="activeTab"
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
