<script setup lang="ts">
import type { HouseholdMember, MealPlan, MealType, AttendanceStatus } from '~/repositories/types'

export type MemberWizardState = {
  memberId: string
  /** Going/Maybe = attending those days; NotGoing = not attending the event at all. */
  dayStatus: AttendanceStatus
  arrival: string
  departure: string
  meals: {
    all: AttendanceStatus
    byType: Partial<Record<MealType, AttendanceStatus>>
  }
}

type CachedState = { stepIndex: number; states: MemberWizardState[] }

const props = defineProps<{
  days: string[]
  householdId: string
  members: HouseholdMember[]
  mealPlansByDay: Record<string, MealPlan[]>
  bulkUpsertDays: (items: { memberId: string, day: string, status: AttendanceStatus }[]) => Promise<boolean>
  bulkUpsertMeals: (items: { planId: string, memberId: string, status: AttendanceStatus }[]) => Promise<boolean>
}>()

const open = defineModel<boolean>('open', { default: false })

const { t } = useI18n()
const { formatDate } = useFormatDate()

const stepIndex = ref(0)
const applying = ref(false)
const memberStates = ref<MemberWizardState[]>([])

// Per-household cache so re-opening restores progress. Cleared after a successful apply.
const stateCache = new Map<string, CachedState>()

const steps = computed(() => [
  { title: t('event.attendanceWizard.stepDates'), slot: 'dates' },
  { title: t('event.attendanceWizard.stepMeals'), slot: 'meals' },
  { title: t('event.attendanceWizard.stepReview'), slot: 'review' },
])

function defaultState(memberId: string): MemberWizardState {
  return {
    memberId,
    dayStatus: 'Going' as AttendanceStatus,
    arrival: props.days[0] ?? '',
    departure: props.days.at(-1) ?? '',
    meals: { all: 'Going' as AttendanceStatus, byType: {} },
  }
}

// Merge cached states with current member list: restore known members, init new ones.
function mergeWithMembers(cached: MemberWizardState[]): MemberWizardState[] {
  const byId = new Map(cached.map(s => [s.memberId, s]))
  return props.members.map(m => byId.get(m.id) ?? defaultState(m.id))
}

watch(open, (isOpen, wasOpen) => {
  if (isOpen) {
    const cached = stateCache.get(props.householdId)
    if (cached) {
      stepIndex.value = cached.stepIndex
      memberStates.value = mergeWithMembers(cached.states)
    }
    else {
      stepIndex.value = 0
      memberStates.value = props.members.map(m => defaultState(m.id))
    }
  }
  else if (wasOpen) {
    // Save progress when dismissed without applying.
    stateCache.set(props.householdId, {
      stepIndex: stepIndex.value,
      states: memberStates.value,
    })
  }
})

// --- Review step helpers ---
const MEAL_ORDER: MealType[] = ['Breakfast', 'Lunch', 'Dinner']

const mealTypeI18nKey: Record<MealType, string> = {
  Breakfast: 'event.meal.breakfast',
  Lunch: 'event.meal.lunch',
  Dinner: 'event.meal.dinner',
}

const statusI18nKey: Record<AttendanceStatus, string> = {
  Going: 'status.going',
  Maybe: 'status.maybe',
  NotGoing: 'status.notGoing',
}

function attendingDays(state: MemberWizardState): string[] {
  const lo = state.arrival <= state.departure ? state.arrival : state.departure
  const hi = state.arrival <= state.departure ? state.departure : state.arrival
  return props.days.filter(d => d >= lo && d <= hi)
}

function reviewDateLine(state: MemberWizardState): string {
  return `${formatDate(state.arrival)} – ${formatDate(state.departure)} · ${t(statusI18nKey[state.dayStatus])}`
}

function mealSummary(state: MemberWizardState): string {
  const types = new Set<MealType>()
  for (const day of attendingDays(state)) {
    for (const plan of props.mealPlansByDay[day] ?? []) types.add(plan.mealType)
  }
  const available = MEAL_ORDER.filter(mt => types.has(mt))
  if (!available.length) return t('event.attendanceWizard.reviewNoMeals')
  return available.map((mt) => {
    const status = state.meals.byType[mt] ?? state.meals.all
    return `${t(mealTypeI18nKey[mt])}: ${t(statusI18nKey[status])}`
  }).join(', ')
}

// --- Apply ---
async function apply() {
  applying.value = true
  try {
    // Collect every change up-front and send two bulk requests (days + meals) rather than one
    // request per member/day/meal, which previously fanned out to hundreds of calls.
    const dayItems: { memberId: string, day: string, status: AttendanceStatus }[] = []
    const mealItems: { planId: string, memberId: string, status: AttendanceStatus }[] = []

    for (const state of memberStates.value) {
      const lo = state.arrival <= state.departure ? state.arrival : state.departure
      const hi = state.arrival <= state.departure ? state.departure : state.arrival

      for (const day of props.days) {
        const inRange = state.dayStatus !== 'NotGoing' && day >= lo && day <= hi
        const dayStatus: AttendanceStatus = inRange ? state.dayStatus : 'NotGoing'
        dayItems.push({ memberId: state.memberId, day, status: dayStatus })

        for (const plan of props.mealPlansByDay[day] ?? []) {
          const mealStatus: AttendanceStatus = inRange
            ? (state.meals.byType[plan.mealType] ?? state.meals.all)
            : 'NotGoing'
          mealItems.push({ planId: plan.id, memberId: state.memberId, status: mealStatus })
        }
      }
    }

    // Failures toast inside the composables and resolve false — keep the modal open so
    // the user's selections aren't lost.
    const [daysOk, mealsOk] = await Promise.all([props.bulkUpsertDays(dayItems), props.bulkUpsertMeals(mealItems)])
    if (!daysOk || !mealsOk) return
    stateCache.delete(props.householdId)
    open.value = false
  }
  finally {
    applying.value = false
  }
}
</script>

<template>
  <UModal v-model:open="open" :title="t('event.attendanceWizard.title')">
    <template #body>
      <div class="space-y-6">
        <!-- Step progress indicator -->
        <UStepper
          v-model="stepIndex"
          :items="steps"
          :linear="false"
          size="sm"
        />

        <p class="text-xs text-muted">
          {{ t('event.attendanceWizard.detailEditReminderShort') }}
        </p>

        <!-- Step content -->
        <GsAttendanceWizardDatesStep
          v-if="stepIndex === 0"
          v-model="memberStates"
          :days="days"
          :members="members"
        />

        <GsAttendanceWizardMealsStep
          v-else-if="stepIndex === 1"
          v-model="memberStates"
          :days="days"
          :members="members"
          :meal-plans-by-day="mealPlansByDay"
        />

        <!-- Review step -->
        <div v-else class="space-y-4">
          <p class="text-sm text-muted">
            {{ t('event.attendanceWizard.rerunWarning') }}
          </p>
          <div class="space-y-3">
            <div
              v-for="state in memberStates"
              :key="state.memberId"
              class="rounded-lg border border-default p-3 space-y-1"
              :class="state.dayStatus === 'NotGoing' ? 'opacity-60' : ''"
            >
              <p class="text-sm font-medium">
                {{ members.find(m => m.id === state.memberId)?.name ?? state.memberId }}
              </p>
              <template v-if="state.dayStatus !== 'NotGoing'">
                <p class="text-xs text-muted">{{ reviewDateLine(state) }}</p>
                <p class="text-xs text-muted">{{ mealSummary(state) }}</p>
              </template>
              <p v-else class="text-xs text-muted">{{ t('status.notGoing') }}</p>
            </div>
          </div>
        </div>
      </div>
    </template>

    <template #footer>
      <div class="flex justify-between w-full gap-2">
        <UButton
          v-if="stepIndex > 0"
          color="neutral"
          variant="outline"
          :disabled="applying"
          @click="() => { stepIndex-- }"
        >
          {{ t('common.back') }}
        </UButton>
        <span v-else />

        <div class="flex gap-2">
          <UButton
            color="neutral"
            variant="ghost"
            :disabled="applying"
            @click="() => { open = false }"
          >
            {{ t('common.cancel') }}
          </UButton>
          <UButton
            v-if="stepIndex < steps.length - 1"
            @click="() => { stepIndex++ }"
          >
            {{ t('common.next') }}
          </UButton>
          <UButton
            v-else
            :loading="applying"
            @click="apply"
          >
            {{ t('event.attendanceWizard.finish') }}
          </UButton>
        </div>
      </div>
    </template>
  </UModal>
</template>
