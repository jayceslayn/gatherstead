<script setup lang="ts">
import type { HouseholdMember, MealPlan, MealType, AttendanceStatus } from '~/repositories/types'
import type { MemberWizardState } from '~/components/GsAttendanceWizardModal.vue'

const props = defineProps<{
  days: string[]
  members: HouseholdMember[]
  mealPlansByDay: Record<string, MealPlan[]>
}>()

const memberStates = defineModel<MemberWizardState[]>({ required: true })

const { t } = useI18n()

const MEAL_ORDER: MealType[] = ['Breakfast', 'Lunch', 'Dinner']

const mealTypeI18nKey: Record<MealType, string> = {
  Breakfast: 'event.meal.breakfast',
  Lunch: 'event.meal.lunch',
  Dinner: 'event.meal.dinner',
}

// Only members who are attending at least one day.
const attendingStates = computed(() => memberStates.value.filter(s => s.dayStatus !== 'NotGoing'))

function memberName(memberId: string): string {
  return props.members.find(m => m.id === memberId)?.name ?? memberId
}

function attendingDays(state: MemberWizardState): string[] {
  const lo = state.arrival <= state.departure ? state.arrival : state.departure
  const hi = state.arrival <= state.departure ? state.departure : state.arrival
  return props.days.filter(d => d >= lo && d <= hi)
}

function availableMealTypes(state: MemberWizardState): MealType[] {
  const types = new Set<MealType>()
  for (const day of attendingDays(state)) {
    for (const plan of props.mealPlansByDay[day] ?? []) types.add(plan.mealType)
  }
  return MEAL_ORDER.filter(mt => types.has(mt))
}

// Derived meal status for display: byType override or fall back to all.
function resolvedMealStatus(state: MemberWizardState, mt: MealType): AttendanceStatus | null {
  return state.meals.byType[mt] ?? state.meals.all ?? null
}

function updateMember(memberId: string, patch: Partial<MemberWizardState>) {
  memberStates.value = memberStates.value.map(s =>
    s.memberId === memberId ? { ...s, ...patch } : s,
  )
}

function setMemberAllMeals(memberId: string, status: AttendanceStatus) {
  updateMember(memberId, { meals: { all: status, byType: {} } })
}

function setMemberMealType(memberId: string, mt: MealType, status: AttendanceStatus) {
  const existing = memberStates.value.find(s => s.memberId === memberId)?.meals ?? { all: 'Going' as AttendanceStatus, byType: {} }
  updateMember(memberId, {
    meals: { ...existing, byType: { ...existing.byType, [mt]: status } },
  })
}

// Household-level bulk controls.
const householdAllMeals = ref<AttendanceStatus | null>(null)
const householdByType = ref<Partial<Record<MealType, AttendanceStatus>>>({})

function applyHouseholdMeals() {
  memberStates.value = memberStates.value.map((s) => {
    if (s.dayStatus === 'NotGoing') return s
    const byType: Partial<Record<MealType, AttendanceStatus>> = {}
    for (const mt of availableMealTypes(s)) {
      const override = householdByType.value[mt]
      const fallback = householdAllMeals.value
      if (override) byType[mt] = override
      else if (fallback) byType[mt] = fallback
    }
    const all = householdAllMeals.value ?? s.meals.all
    return { ...s, meals: { all, byType } }
  })
}

// All meal types available across any attending member.
const allAvailableMealTypes = computed((): MealType[] => {
  const types = new Set<MealType>()
  for (const state of attendingStates.value) {
    for (const mt of availableMealTypes(state)) types.add(mt)
  }
  return MEAL_ORDER.filter(mt => types.has(mt))
})
</script>

<template>
  <div class="space-y-5">
    <div v-if="!attendingStates.length" class="py-6 text-center text-sm text-muted">
      {{ t('event.attendanceWizard.noAttendingMembers') }}
    </div>

    <template v-else>
      <!-- Household-level meal bulk controls -->
      <div class="rounded-lg border border-default bg-elevated p-4 space-y-3">
        <p class="text-sm font-medium">{{ t('event.attendanceWizard.householdDefaults') }}</p>

        <div>
          <p class="text-xs text-muted mb-2">{{ t('event.attendanceWizard.setAllMealsTo') }}</p>
          <GsAttendanceToggle
            :model-value="householdAllMeals"
            size="sm"
            @update:model-value="householdAllMeals = $event; householdByType = {}"
          />
        </div>

        <div v-if="allAvailableMealTypes.length > 1" class="space-y-2">
          <p class="text-xs text-muted">{{ t('event.attendanceModal.orByMealType') }}</p>
          <div
            v-for="mt in allAvailableMealTypes"
            :key="mt"
            class="flex items-center justify-between gap-4"
          >
            <span class="text-sm text-muted">{{ t(mealTypeI18nKey[mt]) }}</span>
            <GsAttendanceToggle
              :model-value="householdByType[mt] ?? null"
              size="xs"
              @update:model-value="householdByType[mt] = $event"
            />
          </div>
        </div>

        <UButton size="sm" variant="outline" @click="applyHouseholdMeals">
          {{ t('event.attendanceWizard.applyToAll') }}
        </UButton>
      </div>

      <!-- Per-member meal rows -->
      <div class="space-y-3">
        <div
          v-for="state in attendingStates"
          :key="state.memberId"
          class="rounded-lg border border-default p-4 space-y-3"
        >
          <p class="text-sm font-medium">
            {{ t('event.attendanceWizard.memberMeals', { name: memberName(state.memberId) }) }}
          </p>

          <div v-if="!availableMealTypes(state).length" class="text-xs text-muted">
            {{ t('event.attendanceWizard.reviewNoMeals') }}
          </div>

          <template v-else>
            <div class="flex items-center justify-between gap-4">
              <span class="text-sm text-muted">{{ t('event.attendanceWizard.allMeals') }}</span>
              <GsAttendanceToggle
                :model-value="state.meals.all"
                size="xs"
                @update:model-value="setMemberAllMeals(state.memberId, $event)"
              />
            </div>

            <div v-if="availableMealTypes(state).length > 1" class="space-y-2 border-t border-default pt-2">
              <p class="text-xs text-muted">{{ t('event.attendanceModal.orByMealType') }}</p>
              <div
                v-for="mt in availableMealTypes(state)"
                :key="mt"
                class="flex items-center justify-between gap-4"
              >
                <span class="text-sm text-muted">{{ t(mealTypeI18nKey[mt]) }}</span>
                <GsAttendanceToggle
                  :model-value="resolvedMealStatus(state, mt)"
                  size="xs"
                  @update:model-value="setMemberMealType(state.memberId, mt, $event)"
                />
              </div>
            </div>
          </template>
        </div>
      </div>
    </template>

  </div>
</template>
