<script setup lang="ts">
import { useEventReport } from '~/composables/useEventReport'
import { useTenantRole } from '~/composables/useTenantRole'
import { useFormatDate } from '~/composables/useFormatDate'
import type { TabsItem } from '@nuxt/ui'

definePageMeta({
  layout: 'default',
})

const { t } = useI18n()
const route = useRoute()
const { isMemberOrAbove } = useTenantRole()

const eventId = computed(() => route.params.eventId as string)
const { report, pending, error } = useEventReport(eventId)
const { formatDate, formatDay } = useFormatDate()

const hasAnyData = computed(() =>
  !!report.value?.days.some(d => d.going > 0 || d.maybe > 0 || d.meals.length > 0),
)

// Tab state — computed so labels re-translate on locale switch. Mirrors the
// event sign-up page so Tasks and Accommodations report sections can slot in.
const tabs = computed<TabsItem[]>(() => [
  { label: t('event.meals'), value: 'meals', slot: 'meals' },
  { label: t('event.tasks'), value: 'tasks', slot: 'tasks' },
  { label: t('event.accommodations'), value: 'accommodations', slot: 'accommodations' },
])

const activeTab = ref<string | number>(tabs.value[0]?.value ?? 0)

watch(activeTab, (newVal) => {
  const tab = tabs.value.find(tb => tb.value === newVal)
  if (tab) {
    history.replaceState(null, '', `#${tab.value}`)
  }
})

onMounted(() => {
  if (tabs.value.some(tab => tab.value === route.hash.substring(1))) {
    activeTab.value = route.hash.substring(1)
  }
})

// Progressive disclosure — meal attendee/dietary detail is collapsed by default.
// Detail stays in the DOM (hidden) so the `print:block` variant can reveal every
// section when printing without juggling expand state.
const expanded = ref<Set<string>>(new Set())
function toggle(id: string) {
  const next = new Set(expanded.value)
  if (next.has(id)) next.delete(id)
  else next.add(id)
  expanded.value = next
}
function isExpanded(id: string) {
  return expanded.value.has(id)
}

function printReport() {
  if (import.meta.client) window.print()
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
        <UButton variant="outline" size="sm" icon="i-heroicons-printer" @click="printReport">
          {{ t('report.event.print') }}
        </UButton>
      </GsPageHeader>

      <div class="flex items-center gap-2 text-sm text-muted mb-6 flex-wrap">
        <UIcon name="i-heroicons-calendar-days" class="size-4 shrink-0" />
        <span>{{ t('event.dateRange', { start: formatDate(report.startDate), end: formatDate(report.endDate) }) }}</span>
      </div>

      <!-- Headline: days of the event and their attendance totals. Attendee and
           dietary detail is reserved for the Meals tab below (progressive disclosure). -->
      <section class="mb-8">
        <h2 class="sr-only">{{ t('report.event.summary') }}</h2>
        <div class="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 gap-3">
          <div
            v-for="day in report.days"
            :key="day.day"
            class="rounded-lg border border-default p-3"
          >
            <p class="font-medium text-sm">{{ formatDay(day.day) }}</p>
            <p class="text-sm text-muted mt-1 inline-flex items-center gap-1">
              <UIcon name="i-heroicons-user-group" class="size-4" />
              {{ t('report.event.attendingCount', { n: day.going }) }}
            </p>
            <p v-if="day.maybe" class="text-xs text-muted">{{ t('report.event.maybeCount', { n: day.maybe }) }}</p>
          </div>
        </div>
      </section>

      <UTabs v-model="activeTab" :items="tabs">
        <template #meals>
          <div class="mt-4">
            <GsEmptyState
              v-if="!hasAnyData"
              icon="i-heroicons-chart-bar"
              :title="t('report.event.noData')"
            />

            <div v-else class="space-y-8">
              <section v-for="day in report.days" :key="day.day">
                <div class="flex items-center justify-between gap-4 mb-3 pb-2 border-b border-default">
                  <h3 class="font-semibold">{{ formatDay(day.day) }}</h3>
                  <div class="flex items-center gap-3 text-sm text-muted">
                    <span class="inline-flex items-center gap-1">
                      <UIcon name="i-heroicons-user-group" class="size-4" />
                      {{ t('report.event.attendingCount', { n: day.going }) }}
                    </span>
                    <span v-if="day.maybe">{{ t('report.event.maybeCount', { n: day.maybe }) }}</span>
                  </div>
                </div>

                <p v-if="!day.meals.length" class="text-sm text-muted">{{ t('report.event.noMeals') }}</p>

                <div v-else class="space-y-3">
                  <UCard v-for="meal in day.meals" :key="meal.mealPlanId">
                    <!-- Collapsed summary: meal, counts, and a dietary cue.
                         Count chips follow the shared status language: Going=success/check,
                         Maybe=secondary/question (color is never the sole differentiator). -->
                    <button
                      type="button"
                      class="w-full flex items-start justify-between gap-3 text-left"
                      :aria-expanded="isExpanded(meal.mealPlanId)"
                      :aria-label="isExpanded(meal.mealPlanId) ? t('report.event.hideDetails') : t('report.event.showDetails')"
                      @click="toggle(meal.mealPlanId)"
                    >
                      <div class="min-w-0">
                        <div class="flex items-center gap-2 flex-wrap">
                          <p class="font-semibold">{{ t(`event.meal.${meal.mealType.toLowerCase()}`) }}</p>
                          <span class="text-xs text-muted truncate">{{ meal.templateName }}</span>
                        </div>
                        <div class="flex flex-wrap gap-2 mt-2">
                          <UBadge color="success" variant="subtle" icon="i-heroicons-check-circle">
                            {{ t('report.event.goingCount', { n: meal.going }) }}
                          </UBadge>
                          <UBadge v-if="meal.maybe" color="secondary" variant="subtle" icon="i-heroicons-question-mark-circle">
                            {{ t('report.event.maybeCount', { n: meal.maybe }) }}
                          </UBadge>
                          <UBadge v-if="meal.bringOwnFood" color="neutral" variant="subtle" icon="i-heroicons-shopping-bag">
                            {{ t('report.event.bringingOwnFood', { n: meal.bringOwnFood }) }}
                          </UBadge>
                          <UBadge v-if="meal.dietary.length" color="primary" variant="subtle" icon="i-heroicons-heart">
                            {{ t('report.event.dietaryCount', { n: meal.dietary.length }) }}
                          </UBadge>
                        </div>
                      </div>
                      <UIcon
                        name="i-heroicons-chevron-down"
                        class="size-5 shrink-0 mt-1 transition-transform print:hidden"
                        :class="isExpanded(meal.mealPlanId) ? 'rotate-180' : ''"
                      />
                    </button>

                    <div :class="['text-sm', isExpanded(meal.mealPlanId) ? 'mt-4 space-y-3' : 'hidden print:block print:mt-4 print:space-y-3']">
                      <div>
                        <p class="text-muted text-xs uppercase tracking-wide mb-1.5">{{ t('report.event.dietaryNeeds') }}</p>
                        <p v-if="!meal.dietary.length" class="text-muted">{{ t('report.event.noDietaryNeeds') }}</p>
                        <div v-else class="flex flex-wrap gap-1.5">
                          <UBadge
                            v-for="d in meal.dietary"
                            :key="d.label"
                            color="primary"
                            variant="subtle"
                          >
                            {{ t('report.event.dietaryTally', { label: d.label, count: d.count }) }}
                          </UBadge>
                        </div>
                      </div>

                      <div v-if="meal.attendees.length">
                        <p class="text-muted text-xs uppercase tracking-wide mb-1.5">{{ t('report.event.attendees') }}</p>
                        <ul class="space-y-0.5">
                          <li
                            v-for="att in meal.attendees"
                            :key="att.memberId"
                            class="flex flex-col gap-0.5"
                          >
                            <div class="flex items-center justify-between gap-2">
                              <span :class="att.status === 'Maybe' ? 'text-muted' : ''">{{ att.name }}</span>
                              <span class="flex items-center gap-1.5">
                                <GsStatusBadge :status="att.status" icon-only />
                                <UBadge v-if="att.bringOwnFood" color="neutral" variant="subtle" icon="i-heroicons-shopping-bag">
                                  {{ t('report.event.ownFood') }}
                                </UBadge>
                              </span>
                            </div>
                            <p v-if="att.dietaryNotes" class="text-xs text-muted italic pl-0.5">
                              {{ att.dietaryNotes }}
                            </p>
                          </li>
                        </ul>
                      </div>
                    </div>
                  </UCard>
                </div>
              </section>
            </div>
          </div>
        </template>

        <template #tasks>
          <div class="mt-4">
            <GsEmptyState
              icon="i-heroicons-clipboard-document-list"
              :title="t('report.event.comingSoon')"
            />
          </div>
        </template>

        <template #accommodations>
          <div class="mt-4">
            <GsEmptyState
              icon="i-heroicons-home"
              :title="t('report.event.comingSoon')"
            />
          </div>
        </template>
      </UTabs>
    </template>

    <GsEmptyState
      v-else
      icon="i-heroicons-exclamation-triangle"
      :title="t('error.notFound')"
    />
  </div>
</template>
