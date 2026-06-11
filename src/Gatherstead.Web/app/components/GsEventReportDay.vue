<script setup lang="ts">
import type { EventReportDay } from '~/repositories/types'
import { taskCoverage, accommodationOccupancy } from '~/composables/useReportView'

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

const accommodationTypeIcon: Record<string, string> = {
  Bedroom: 'i-heroicons-home',
  Bunk: 'i-heroicons-rectangle-stack',
  RvPad: 'i-heroicons-truck',
  Tent: 'i-heroicons-map',
  Offsite: 'i-heroicons-arrow-top-right-on-square',
}

const occupancyColor: Record<string, 'neutral' | 'success' | 'warning' | 'error'> = {
  vacant: 'neutral',
  partial: 'success',
  full: 'warning',
  over: 'error',
  unknown: 'neutral',
}

const showMeals = computed(() => props.forcePrint || props.section === 'meals')
const showTasks = computed(() => props.forcePrint || props.section === 'tasks')
const showAccommodations = computed(() => props.forcePrint || props.section === 'accommodations')

function isExpanded(id: string) {
  return props.expanded.has(id)
}
// Detail stays in the DOM (hidden) so the print variant can reveal it without juggling state.
function detailClass(id: string, expandedClasses: string) {
  return isExpanded(id) ? expandedClasses : 'hidden print:block print:mt-3 print:space-y-2'
}

function occupancy(acc: typeof props.day.accommodations[number]) {
  return accommodationOccupancy(acc)
}
</script>

<template>
  <section class="flex flex-col">
    <!-- Headline: date + total attendance. Stays visible while the day's detail scrolls. -->
    <header class="sticky top-0 z-10 bg-default border-b border-default pb-2 mb-3">
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
          <UCard v-for="meal in day.meals" :key="meal.mealPlanId" :ui="{ body: 'p-3 sm:p-3' }">
            <button
              type="button"
              class="w-full flex items-start justify-between gap-3 text-left"
              :aria-expanded="isExpanded(meal.mealPlanId)"
              :aria-label="isExpanded(meal.mealPlanId) ? t('report.event.hideDetails') : t('report.event.showDetails')"
              @click="emit('toggle', meal.mealPlanId)"
            >
              <div class="min-w-0">
                <div class="flex items-center gap-2 flex-wrap">
                  <p class="font-semibold">{{ t(`event.meal.${meal.mealType.toLowerCase()}`) }}</p>
                  <span class="text-xs text-muted truncate">{{ meal.templateName }}</span>
                </div>
                <div class="flex flex-wrap gap-1.5 mt-2">
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

            <div :class="['text-sm', detailClass(meal.mealPlanId, 'mt-3 space-y-3')]">
              <div>
                <p class="text-muted text-xs uppercase tracking-wide mb-1.5">{{ t('report.event.dietaryNeeds') }}</p>
                <p v-if="!meal.dietary.length" class="text-muted">{{ t('report.event.noDietaryNeeds') }}</p>
                <div v-else class="flex flex-wrap gap-1.5">
                  <UBadge v-for="d in meal.dietary" :key="d.label" color="primary" variant="subtle">
                    {{ t('report.event.dietaryTally', { label: d.label, count: d.count }) }}
                  </UBadge>
                </div>
              </div>

              <div v-if="meal.attendees.length">
                <p class="text-muted text-xs uppercase tracking-wide mb-1.5">{{ t('report.event.attendees') }}</p>
                <ul class="space-y-0.5">
                  <li v-for="att in meal.attendees" :key="att.memberId" class="flex flex-col gap-0.5">
                    <div class="flex items-center justify-between gap-2">
                      <span :class="att.status === 'Maybe' ? 'text-muted' : ''">{{ att.name }}</span>
                      <span class="flex items-center gap-1.5">
                        <GsStatusBadge :status="att.status" icon-only />
                        <UBadge v-if="att.bringOwnFood" color="neutral" variant="subtle" icon="i-heroicons-shopping-bag">
                          {{ t('report.event.ownFood') }}
                        </UBadge>
                      </span>
                    </div>
                    <p v-if="att.dietaryNotes" class="text-xs text-muted italic pl-0.5">{{ att.dietaryNotes }}</p>
                  </li>
                </ul>
              </div>
            </div>
          </UCard>
        </div>
      </div>

      <!-- ── Tasks ─────────────────────────────────────────────── -->
      <div v-if="showTasks">
        <p v-if="forcePrint" class="text-xs font-medium uppercase tracking-wide text-muted mb-2">{{ t('event.tasks') }}</p>
        <p v-if="!day.tasks.length" class="text-sm text-muted">{{ t('report.event.noTasks') }}</p>
        <div v-else class="space-y-2">
          <UCard v-for="task in day.tasks" :key="task.taskPlanId" :ui="{ body: 'p-3 sm:p-3' }">
            <button
              type="button"
              class="w-full flex items-start justify-between gap-3 text-left"
              :aria-expanded="isExpanded(task.taskPlanId)"
              :aria-label="isExpanded(task.taskPlanId) ? t('report.event.hideDetails') : t('report.event.showDetails')"
              @click="emit('toggle', task.taskPlanId)"
            >
              <div class="min-w-0">
                <div class="flex items-center gap-2 flex-wrap">
                  <p class="font-semibold">{{ task.templateName }}</p>
                  <span v-if="task.timeSlot" class="text-xs text-muted">{{ t(`event.task.${task.timeSlot.toLowerCase()}`) }}</span>
                </div>
                <div class="flex flex-wrap items-center gap-1.5 mt-2">
                  <GsStatusBadge :status="taskCoverage(task)" />
                  <UBadge color="neutral" variant="subtle" icon="i-heroicons-user-group">
                    {{ task.minimumAssignees != null
                      ? t('report.event.assigneeRatio', { n: task.assigneeCount, m: task.minimumAssignees })
                      : t('report.event.assigneeCount', { n: task.assigneeCount }) }}
                  </UBadge>
                </div>
              </div>
              <UIcon
                name="i-heroicons-chevron-down"
                class="size-5 shrink-0 mt-1 transition-transform print:hidden"
                :class="isExpanded(task.taskPlanId) ? 'rotate-180' : ''"
              />
            </button>

            <div :class="['text-sm', detailClass(task.taskPlanId, 'mt-3 space-y-2')]">
              <p v-if="task.isException && task.exceptionReason" class="text-muted italic">
                {{ task.exceptionReason }}
              </p>
              <div v-if="task.assignees.length">
                <p class="text-muted text-xs uppercase tracking-wide mb-1.5">{{ t('report.event.assignees') }}</p>
                <ul class="space-y-0.5">
                  <li v-for="(name, i) in task.assignees" :key="i">{{ name }}</li>
                </ul>
              </div>
              <p v-else class="text-muted">{{ t('report.event.noAssignees') }}</p>
            </div>
          </UCard>
        </div>
      </div>

      <!-- ── Accommodations ────────────────────────────────────── -->
      <div v-if="showAccommodations">
        <p v-if="forcePrint" class="text-xs font-medium uppercase tracking-wide text-muted mb-2">{{ t('event.accommodations') }}</p>
        <p v-if="!day.accommodations.length" class="text-sm text-muted">{{ t('report.event.noAccommodations') }}</p>
        <div v-else class="space-y-2">
          <UCard v-for="acc in day.accommodations" :key="acc.accommodationId" :ui="{ body: 'p-3 sm:p-3' }">
            <button
              type="button"
              class="w-full flex items-start justify-between gap-3 text-left"
              :aria-expanded="isExpanded(acc.accommodationId)"
              :aria-label="isExpanded(acc.accommodationId) ? t('report.event.hideDetails') : t('report.event.showDetails')"
              @click="emit('toggle', acc.accommodationId)"
            >
              <div class="min-w-0 flex items-start gap-2">
                <UIcon :name="accommodationTypeIcon[acc.type] ?? 'i-heroicons-home'" class="size-5 shrink-0 mt-0.5 text-primary" />
                <div class="min-w-0">
                  <p class="font-semibold truncate">{{ acc.name }}</p>
                  <div class="mt-2">
                    <UBadge
                      :color="occupancyColor[occupancy(acc).state]"
                      variant="subtle"
                      icon="i-heroicons-user-group"
                    >
                      {{ occupancy(acc).capacity != null
                        ? t('report.event.occupancy', { n: acc.occupied, m: occupancy(acc).capacity })
                        : t('report.event.occupantCount', { n: acc.occupied }) }}
                    </UBadge>
                  </div>
                  <p v-if="acc.notes" class="text-xs text-muted mt-1.5 line-clamp-2 print:line-clamp-none">{{ acc.notes }}</p>
                </div>
              </div>
              <UIcon
                name="i-heroicons-chevron-down"
                class="size-5 shrink-0 mt-1 transition-transform print:hidden"
                :class="isExpanded(acc.accommodationId) ? 'rotate-180' : ''"
              />
            </button>

            <div :class="['text-sm', detailClass(acc.accommodationId, 'mt-3')]">
              <ul class="space-y-0.5">
                <li v-for="occ in acc.occupants" :key="occ.memberId" class="flex items-center justify-between gap-2">
                  <span>{{ occ.name }}</span>
                  <span class="flex items-center gap-1.5">
                    <span v-if="occ.partySize" class="text-xs text-muted">{{ t('accommodation.partySizeValue', { n: occ.partySize }) }}</span>
                    <GsStatusBadge :status="occ.status" icon-only />
                  </span>
                </li>
              </ul>
            </div>
          </UCard>
        </div>
      </div>
    </div>
  </section>
</template>
