<script setup lang="ts">
// Shared "limit to a date sub-range" control for the meal/task template modals: a toggle that
// reveals a start/end date pair. Three independent v-models keep the parent's reactive form the
// source of truth; date-range validation stays in the parent (which owns the error message).
defineProps<{
  error?: string
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

    <div v-if="useSubRange" class="grid grid-cols-2 gap-4 mt-4">
      <UFormField :label="t('event.startDate')">
        <UInput v-model="startDate" type="date" class="w-full" />
      </UFormField>
      <UFormField :label="t('event.endDate')">
        <UInput v-model="endDate" type="date" class="w-full" />
      </UFormField>
    </div>
    <p v-if="error" class="text-sm text-error mt-1">{{ error }}</p>
  </div>
</template>
