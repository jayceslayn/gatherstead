<script setup lang="ts">
// Shared "limit to a date sub-range" control for the meal/task template modals: a toggle that
// reveals a start/end date pair. Three independent v-models keep the parent's reactive form the
// source of truth; date-range validation stays in the parent (which owns the error message).
defineProps<{
  error?: string
  /** Optional ISO bounds constraining the sub-range to the event span. */
  min?: string
  max?: string
}>()

const useSubRange = defineModel<boolean>('useSubRange', { default: false })
const startDate = defineModel<string>('startDate', { default: '' })
const endDate = defineModel<string>('endDate', { default: '' })

const { t } = useI18n()
</script>

<template>
  <div>
    <UFormField>
      <UCheckbox v-model="useSubRange" :label="t('event.meal.useSubRange')" />
    </UFormField>

    <UFormField v-if="useSubRange" :label="t('event.dateRangeLabel')" class="mt-4">
      <GsDateRangePicker
        v-model:start-date="startDate"
        v-model:end-date="endDate"
        :min="min"
        :max="max"
      />
    </UFormField>
    <p v-if="error" class="text-sm text-error mt-1">{{ error }}</p>
  </div>
</template>
