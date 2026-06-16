<script setup lang="ts">
import type { EventReportDay } from '~/repositories/types'

type Section = 'meals' | 'tasks' | 'accommodations'

const props = defineProps<{
  day: EventReportDay
  // The single section to render on screen. Ignored when forcePrint is set.
  section?: Section
  expanded: Set<string>
  // When true (print stack), every section renders and detail prints expanded.
  forcePrint?: boolean
}>()

const emit = defineEmits<{ toggle: [id: string] }>()

const { t } = useI18n()
const { formatDay } = useFormatDate()

const showMeals = computed(() => props.forcePrint || props.section === 'meals')
const showTasks = computed(() => props.forcePrint || props.section === 'tasks')
const showAccommodations = computed(() => props.forcePrint || props.section === 'accommodations')
</script>

<template>
  <section class="flex flex-col print:break-inside-avoid">
    <!-- Headline: date + total attendance. Stays visible while the day's detail scrolls.
         In print the sticky positioning is dropped and the header is kept with its cards. -->
    <header class="sticky top-0 z-10 bg-default border-b border-default pb-2 mb-3 print:static print:break-after-avoid">
      <h3 class="font-semibold text-highlighted">{{ formatDay(day.day) }}</h3>
      <div class="flex items-center gap-3 text-sm text-muted mt-0.5">
        <span class="inline-flex items-center gap-1">
          <UIcon name="i-heroicons-user-group" class="size-4" />
          {{ t('report.event.attendingCount', { n: day.going }) }}
        </span>
        <span v-if="day.maybe">{{ t('report.event.maybeCount', { n: day.maybe }) }}</span>
      </div>
    </header>

    <div class="space-y-5">
      <!-- ── Meals ─────────────────────────────────────────────── -->
      <div v-if="showMeals">
        <p v-if="forcePrint" class="text-xs font-medium uppercase tracking-wide text-muted mb-2">{{ t('event.meals') }}</p>
        <p v-if="!day.meals.length" class="text-sm text-muted">{{ t('report.event.noMeals') }}</p>
        <div v-else class="space-y-2">
          <GsReportMealCell
            v-for="meal in day.meals"
            :key="meal.mealPlanId"
            :meal="meal"
            :expanded="expanded"
            @toggle="emit('toggle', $event)"
          />
        </div>
      </div>

      <!-- ── Tasks ─────────────────────────────────────────────── -->
      <div v-if="showTasks">
        <p v-if="forcePrint" class="text-xs font-medium uppercase tracking-wide text-muted mb-2">{{ t('event.tasks') }}</p>
        <p v-if="!day.tasks.length" class="text-sm text-muted">{{ t('report.event.noTasks') }}</p>
        <div v-else class="space-y-2">
          <GsReportTaskCell
            v-for="task in day.tasks"
            :key="task.taskPlanId"
            :task="task"
            :expanded="expanded"
            @toggle="emit('toggle', $event)"
          />
        </div>
      </div>

      <!-- ── Accommodations ────────────────────────────────────── -->
      <div v-if="showAccommodations">
        <p v-if="forcePrint" class="text-xs font-medium uppercase tracking-wide text-muted mb-2">{{ t('event.accommodations') }}</p>
        <p v-if="!day.accommodations.length" class="text-sm text-muted">{{ t('report.event.noAccommodations') }}</p>
        <div v-else class="space-y-2">
          <GsReportAccommodationCell
            v-for="acc in day.accommodations"
            :key="acc.accommodationId"
            :acc="acc"
            :day="day.day"
            :expanded="expanded"
            @toggle="emit('toggle', $event)"
          />
        </div>
      </div>
    </div>
  </section>
</template>
