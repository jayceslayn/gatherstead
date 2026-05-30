<script setup lang="ts">
import { useProperties } from '~/composables/useProperties'

definePageMeta({
  layout: 'default',
})

const { t } = useI18n()
const { properties, pending: propertiesPending } = useProperties()
const { refresh } = useEvents()
const { updating, createEvent } = useEventActions(refresh)

const saving = computed(() => updating.value.includes('new'))

const form = reactive({
  propertyId: '',
  name: '',
  startDate: '',
  endDate: '',
})

const errors = reactive({
  propertyId: '',
  name: '',
  dates: '',
})

const propertyItems = computed(() =>
  properties.value.map(p => ({ label: p.name, value: p.id })),
)

watchEffect(() => {
  const first = properties.value[0]
  if (!form.propertyId && first) form.propertyId = first.id
})

function validate(): boolean {
  errors.propertyId = form.propertyId ? '' : t('validation.required', { field: t('property.title') })
  errors.name = form.name.trim() ? '' : t('validation.required', { field: t('event.name') })
  errors.dates = ''
  if (!form.startDate || !form.endDate) {
    errors.dates = t('validation.required', { field: t('event.dateRangeLabel') })
  }
  else if (form.endDate < form.startDate) {
    errors.dates = t('event.endBeforeStart')
  }
  return !errors.propertyId && !errors.name && !errors.dates
}

async function onSubmit() {
  if (!validate()) return
  const created = await createEvent(form.propertyId, form.name.trim(), form.startDate, form.endDate)
  if (created) {
    await navigateTo(`/app/events/${created.id}`)
  }
}
</script>

<template>
  <div>
    <GsBreadcrumb
      :items="[
        { label: t('event.title'), to: '/app/events' },
        { label: t('event.createTitle') },
      ]"
    />

    <GsPageHeader :title="t('event.createTitle')" />

    <div v-if="!propertiesPending && !properties.length">
      <GsEmptyState
        icon="i-heroicons-building-office-2"
        :title="t('event.noPropertiesTitle')"
        :description="t('event.noPropertiesHint')"
      >
        <UButton to="/app/properties" icon="i-heroicons-building-office-2">
          {{ t('property.title') }}
        </UButton>
      </GsEmptyState>
    </div>

    <UForm v-else :state="form" class="max-w-lg space-y-5" @submit="onSubmit">
      <UFormField :label="t('event.name')" name="name" :error="errors.name || undefined" required>
        <UInput v-model="form.name" :placeholder="t('event.name')" required class="w-full" />
      </UFormField>

      <UFormField :label="t('property.title')" name="propertyId" :error="errors.propertyId || undefined" required>
        <USelect v-model="form.propertyId" :items="propertyItems" class="w-full" />
      </UFormField>

      <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <UFormField :label="t('event.startDate')" name="startDate" required>
          <UInput v-model="form.startDate" type="date" class="w-full" />
        </UFormField>
        <UFormField :label="t('event.endDate')" name="endDate" required>
          <UInput v-model="form.endDate" type="date" class="w-full" />
        </UFormField>
      </div>
      <p v-if="errors.dates" class="text-sm text-error -mt-2">{{ errors.dates }}</p>

      <div class="flex items-center gap-3 pt-2">
        <UButton type="submit" :loading="saving">
          {{ t('common.create') }}
        </UButton>
        <UButton variant="ghost" to="/app/events">
          {{ t('common.cancel') }}
        </UButton>
      </div>
    </UForm>
  </div>
</template>
