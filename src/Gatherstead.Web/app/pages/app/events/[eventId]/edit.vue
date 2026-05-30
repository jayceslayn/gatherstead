<script setup lang="ts">
import type { EventSummary } from '~/repositories/types'
import { useProperties } from '~/composables/useProperties'

definePageMeta({
  layout: 'default',
})

const { t } = useI18n()
const route = useRoute()
const router = useRouter()

const eventId = computed(() => route.params.eventId as string)
const { event, pending, refresh } = useEvent(eventId)
const { properties } = useProperties()
const { refresh: refreshList } = useEvents()
const { updating, updateEvent, deleteEvent } = useEventActions(refreshList)

const saving = computed(() => updating.value.includes(eventId.value))
const showDeleteConfirm = ref(false)

const form = reactive({
  propertyId: '',
  name: '',
  startDate: '',
  endDate: '',
})

const errors = reactive({ name: '', dates: '' })

const propertyItems = computed(() =>
  properties.value.map(p => ({ label: p.name, value: p.id })),
)

watch(event, (val: EventSummary | null) => {
  if (!val) return
  form.propertyId = val.propertyId
  form.name = val.name
  form.startDate = val.startDate
  form.endDate = val.endDate
}, { immediate: true })

function validate(): boolean {
  errors.name = form.name.trim() ? '' : t('validation.required', { field: t('event.name') })
  errors.dates = ''
  if (!form.startDate || !form.endDate) {
    errors.dates = t('validation.required', { field: t('event.dateRangeLabel') })
  }
  else if (form.endDate < form.startDate) {
    errors.dates = t('event.endBeforeStart')
  }
  return !errors.name && !errors.dates
}

async function onSubmit() {
  if (!validate()) return
  const ok = await updateEvent(eventId.value, form.name.trim(), form.startDate, form.endDate)
  if (ok) {
    await refresh()
    await navigateTo(`/app/events/${eventId.value}`)
  }
}

async function confirmDelete() {
  showDeleteConfirm.value = false
  await deleteEvent(eventId.value)
  await router.push('/app/events')
}
</script>

<template>
  <div>
    <div v-if="pending" class="py-16 text-center">
      <p class="text-muted">{{ t('common.loading') }}</p>
    </div>

    <template v-else-if="event">
      <GsBreadcrumb
        :items="[
          { label: t('event.title'), to: '/app/events' },
          { label: event.name, to: `/app/events/${eventId}` },
          { label: t('common.edit') },
        ]"
      />

      <GsPageHeader :title="`${t('common.edit')} ${event.name}`" />

      <UForm :state="form" class="max-w-lg space-y-5" @submit="onSubmit">
        <UFormField :label="t('event.name')" name="name" :error="errors.name || undefined" required>
          <UInput v-model="form.name" :placeholder="t('event.name')" required class="w-full" />
        </UFormField>

        <UFormField :label="t('property.title')" name="propertyId" :hint="t('event.propertyLocked')">
          <USelect v-model="form.propertyId" :items="propertyItems" disabled class="w-full" />
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

        <p class="text-xs text-muted">{{ t('event.dateChangeHint') }}</p>

        <div class="flex items-center gap-3 pt-2">
          <UButton type="submit" :loading="saving">
            {{ t('common.save') }}
          </UButton>
          <UButton variant="ghost" :to="`/app/events/${eventId}`">
            {{ t('common.cancel') }}
          </UButton>
        </div>
      </UForm>

      <GsRoleGate min-role="Manager">
        <div class="mt-12 pt-6 border-t border-default">
          <UButton
            color="error"
            variant="ghost"
            icon="i-heroicons-trash"
            @click="showDeleteConfirm = true"
          >
            {{ t('event.deleteTitle') }}
          </UButton>
        </div>
      </GsRoleGate>
    </template>

    <GsEmptyState
      v-else
      icon="i-heroicons-exclamation-triangle"
      :title="t('error.notFound')"
    />

    <GsConfirmModal
      v-model:open="showDeleteConfirm"
      :title="t('event.deleteTitle')"
      :description="t('event.deleteConfirm')"
      :confirm-label="t('common.delete')"
      danger
      @confirm="confirmDelete"
    />
  </div>
</template>
