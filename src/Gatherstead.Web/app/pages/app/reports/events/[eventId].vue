<script setup lang="ts">
import { useEventReport } from '~/composables/useEventReport'
import { useTenantRole } from '~/composables/useTenantRole'
import { useFormatDate } from '~/composables/useFormatDate'

definePageMeta({
  layout: 'default',
})

const { t } = useI18n()
const route = useRoute()
const { isMemberOrAbove } = useTenantRole()

const eventId = computed(() => route.params.eventId as string)
const { report, pending, error } = useEventReport(eventId)
const { formatDay } = useFormatDate()

const hasAnyData = computed(() =>
  !!report.value?.days.some(d => d.going > 0 || d.maybe > 0 || d.meals.length > 0),
)

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

      <p class="text-sm text-muted mb-6">{{ t('report.event.title') }}</p>

      <GsEmptyState
        v-if="!hasAnyData"
        icon="i-heroicons-chart-bar"
        :title="t('report.event.noData')"
      />

      <div v-else class="space-y-8">
        <section v-for="day in report.days" :key="day.day">
          <div class="flex items-center justify-between gap-4 mb-3 pb-2 border-b border-default">
            <h2 class="font-semibold">{{ formatDay(day.day) }}</h2>
            <div class="flex items-center gap-3 text-sm text-muted">
              <span class="inline-flex items-center gap-1">
                <UIcon name="i-heroicons-user-group" class="size-4" />
                {{ t('report.event.attendingCount', { n: day.going }) }}
              </span>
              <span v-if="day.maybe">{{ t('report.event.maybeCount', { n: day.maybe }) }}</span>
            </div>
          </div>

          <p v-if="!day.meals.length" class="text-sm text-muted">{{ t('report.event.noMeals') }}</p>

          <div v-else class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            <UCard v-for="meal in day.meals" :key="meal.mealPlanId">
              <template #header>
                <div class="flex items-center justify-between gap-2">
                  <p class="font-semibold">{{ t(`event.meal.${meal.mealType.toLowerCase()}`) }}</p>
                  <span class="text-xs text-muted truncate">{{ meal.templateName }}</span>
                </div>
              </template>

              <div class="space-y-3 text-sm">
                <!-- Count chips follow the shared status language: Going=success/check,
                     Maybe=secondary/question (color is never the sole differentiator). -->
                <div class="flex flex-wrap gap-2">
                  <UBadge color="success" variant="subtle" icon="i-heroicons-check-circle">
                    {{ t('report.event.goingCount', { n: meal.going }) }}
                  </UBadge>
                  <UBadge v-if="meal.maybe" color="secondary" variant="subtle" icon="i-heroicons-question-mark-circle">
                    {{ t('report.event.maybeCount', { n: meal.maybe }) }}
                  </UBadge>
                  <UBadge v-if="meal.bringOwnFood" color="neutral" variant="subtle" icon="i-heroicons-shopping-bag">
                    {{ t('report.event.bringingOwnFood', { n: meal.bringOwnFood }) }}
                  </UBadge>
                </div>

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
    </template>

    <GsEmptyState
      v-else
      icon="i-heroicons-exclamation-triangle"
      :title="t('error.notFound')"
    />
  </div>
</template>
