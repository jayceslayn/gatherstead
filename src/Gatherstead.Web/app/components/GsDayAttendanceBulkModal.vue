<script setup lang="ts">
import type { AttendanceStatus, MealType } from '~/repositories/types'

const props = defineProps<{
  contextLine: string
  mealTypes: MealType[]
}>()

const open = defineModel<boolean>('open', { default: false })

const emit = defineEmits<{
  apply: [assignments: Partial<Record<MealType, AttendanceStatus>>]
}>()

const { t } = useI18n()

const assignments = ref<Partial<Record<MealType, AttendanceStatus>>>({})

watch(open, (val) => {
  if (val) assignments.value = {}
})

function setAll(status: AttendanceStatus) {
  const next: Partial<Record<MealType, AttendanceStatus>> = {}
  for (const mt of props.mealTypes) next[mt] = status
  assignments.value = next
}

function apply() {
  emit('apply', { ...assignments.value })
  open.value = false
}

const mealTypeI18nKey: Record<MealType, string> = {
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
          <p class="font-semibold text-base">{{ t('event.attendanceModal.bulkMeals') }}</p>
          <p class="text-sm text-muted">{{ contextLine }}</p>
        </div>

        <div>
          <p class="text-sm font-medium mb-2">{{ t('event.attendanceModal.setAllTo') }}</p>
          <GsAttendanceToggle
            :model-value="null"
            @update:model-value="setAll($event)"
          />
        </div>

        <div v-if="mealTypes.length > 1">
          <p class="text-sm font-medium mb-2">{{ t('event.attendanceModal.orByMealType') }}</p>
          <div class="space-y-3">
            <div
              v-for="mt in mealTypes"
              :key="mt"
              class="flex items-center justify-between gap-4"
            >
              <span class="text-sm text-muted">{{ t(mealTypeI18nKey[mt]) }}</span>
              <GsAttendanceToggle
                :model-value="assignments[mt] ?? null"
                size="xs"
                @update:model-value="assignments[mt] = $event"
              />
            </div>
          </div>
        </div>

        <div class="flex justify-between pt-1">
          <UButton color="neutral" variant="ghost" @click="open = false">
            {{ t('event.attendanceModal.skipMeals') }}
          </UButton>
          <UButton @click="apply">{{ t('common.apply') }}</UButton>
        </div>
      </div>
    </template>
  </UModal>
</template>
