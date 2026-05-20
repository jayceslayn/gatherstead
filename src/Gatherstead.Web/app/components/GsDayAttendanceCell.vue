<script setup lang="ts">
import type { AttendanceStatus, HouseholdMember, MealPlan } from '~/repositories/types'

defineProps<{
  member: HouseholdMember
  day: string
  dayStatus: AttendanceStatus | null
  dayLoading: boolean
  mealPlans: MealPlan[]
  mealStatuses: Record<string, AttendanceStatus | undefined>
}>()

const emit = defineEmits<{
  open: []
}>()

const statusIcon: Record<AttendanceStatus, string> = {
  Going: 'i-heroicons-check-circle-20-solid',
  Maybe: 'i-heroicons-question-mark-circle-20-solid',
  NotGoing: 'i-heroicons-x-circle-20-solid',
}

const statusClass: Record<AttendanceStatus, string> = {
  Going: 'text-success',
  Maybe: 'text-muted',
  NotGoing: 'text-error',
}

const MEAL_LETTER: Record<string, string> = { Breakfast: 'B', Lunch: 'L', Dinner: 'D' }
</script>

<template>
  <button
    class="flex flex-col items-center justify-center gap-0.5 w-32 min-w-32 py-1.5 px-2 rounded hover:bg-(--ui-elevated) transition-colors cursor-pointer"
    @click="emit('open')"
  >
    <div class="flex items-center justify-center h-5">
      <template v-if="dayLoading">
        <UIcon name="i-heroicons-arrow-path" class="size-4 animate-spin text-muted" />
      </template>
      <template v-else-if="dayStatus">
        <UIcon :name="statusIcon[dayStatus]" :class="['size-4', statusClass[dayStatus]]" />
      </template>
      <template v-else>
        <span class="text-xs text-muted/50">—</span>
      </template>
    </div>

    <div v-if="mealPlans.length" class="flex items-center gap-1.5">
      <div
        v-for="plan in mealPlans"
        :key="plan.id"
        class="flex items-center gap-0.5"
        :class="mealStatuses[plan.id] ? statusClass[mealStatuses[plan.id]!] : 'text-muted/40'"
      >
        <span class="text-[9px] font-medium leading-none">{{ MEAL_LETTER[plan.mealType] ?? plan.mealType[0] }}</span>
        <UIcon v-if="mealStatuses[plan.id]" :name="statusIcon[mealStatuses[plan.id]!]" class="size-3" />
        <span v-else class="text-[9px] leading-none">—</span>
      </div>
    </div>
  </button>
</template>
