<script setup lang="ts">
import type { AttendanceStatus, HouseholdMember, MealPlan } from '~/repositories/types'

const props = defineProps<{
  member: HouseholdMember
  day: string
  dayStatus: AttendanceStatus | null
  dayLoading: boolean
  mealPlans: MealPlan[]
  mealStatuses: Record<string, AttendanceStatus | undefined>
  mealLoading: Record<string, boolean>
}>()

const open = defineModel<boolean>('open', { default: false })

const emit = defineEmits<{
  'set-day': [status: AttendanceStatus]
  'set-meal': [planId: string, status: AttendanceStatus]
}>()

const { t } = useI18n()

const dayLabel = computed(() =>
  new Intl.DateTimeFormat(undefined, { weekday: 'long', month: 'long', day: 'numeric' }).format(
    new Date(props.day + 'T00:00:00'),
  ),
)

const mealsDisabled = computed(() => props.dayStatus === 'NotGoing')

const mealTypeI18nKey: Record<string, string> = {
  Breakfast: 'event.meal.breakfast',
  Lunch: 'event.meal.lunch',
  Dinner: 'event.meal.dinner',
}
</script>

<template>
  <UModal v-model:open="open">
    <template #content>
      <div class="p-6 space-y-5">
        <div>
          <p class="font-semibold text-base">{{ member.name }}</p>
          <p class="text-sm text-muted">{{ dayLabel }}</p>
        </div>

        <div>
          <p class="text-sm font-medium mb-2">{{ t('event.attendanceModal.attendingDay') }}</p>
          <GsAttendanceToggle
            :model-value="dayStatus"
            :loading="dayLoading"
            @update:model-value="emit('set-day', $event)"
          />
        </div>

        <div v-if="mealPlans.length">
          <p class="text-sm font-medium mb-2">{{ t('event.attendanceModal.meals') }}</p>
          <div class="space-y-3" :class="{ 'opacity-50 pointer-events-none': mealsDisabled }">
            <div
              v-for="plan in mealPlans"
              :key="plan.id"
              class="flex items-center justify-between gap-4"
            >
              <span class="text-sm text-muted">{{ t(mealTypeI18nKey[plan.mealType] ?? plan.mealType) }}</span>
              <GsAttendanceToggle
                :model-value="mealStatuses[plan.id] ?? null"
                :loading="mealLoading[plan.id] ?? false"
                size="xs"
                @update:model-value="emit('set-meal', plan.id, $event)"
              />
            </div>
          </div>
          <p v-if="mealsDisabled" class="text-xs text-muted mt-2">
            {{ t('event.attendanceModal.mealsDisabledHint') }}
          </p>
        </div>

        <div class="flex justify-end pt-1">
          <UButton @click="open = false">{{ t('common.done') }}</UButton>
        </div>
      </div>
    </template>
  </UModal>
</template>
