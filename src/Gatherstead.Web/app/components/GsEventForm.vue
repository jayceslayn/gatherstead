<script setup lang="ts">
const name = defineModel<string>('name', { required: true })
const startDate = defineModel<string>('startDate', { required: true })
const endDate = defineModel<string>('endDate', { required: true })
const propertyId = defineModel<string>('propertyId', { required: true })

defineProps<{
  propertyItems: { label: string, value: string }[]
  propertyLocked?: boolean
  errors: { name: string, dates: string }
}>()

const { t } = useI18n()
</script>

<template>
  <div class="space-y-5">
    <UFormField :label="t('event.name')" name="name" :error="errors.name || undefined" required>
      <UInput v-model="name" :placeholder="t('event.name')" required class="w-full" />
    </UFormField>

    <UFormField
      :label="t('property.title')"
      name="propertyId"
      :hint="propertyLocked ? t('event.propertyLocked') : undefined"
    >
      <USelect v-model="propertyId" :items="propertyItems" :disabled="propertyLocked" class="w-full" />
    </UFormField>

    <UFormField :label="t('event.dateRangeLabel')" name="dates" required>
      <GsDateRangePicker v-model:start-date="startDate" v-model:end-date="endDate" />
    </UFormField>
    <p v-if="errors.dates" class="text-sm text-error -mt-2">{{ errors.dates }}</p>
  </div>
</template>
