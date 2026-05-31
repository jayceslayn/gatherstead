<script setup lang="ts">
import type { PropertySummary } from '~/repositories/types'
import { useProperty, usePropertyActions } from '~/composables/useProperties'

definePageMeta({
  layout: 'default',
})

const { t } = useI18n()
const route = useRoute()
const router = useRouter()

const propertyId = computed(() => route.params.propertyId as string)
const { property, pending, refresh } = useProperty(propertyId)
const { updating, updateProperty, deleteProperty } = usePropertyActions(refresh)

const name = ref('')
const nameError = ref('')
const showDeleteConfirm = ref(false)

const saving = computed(() => updating.value.includes(propertyId.value))

watch(property, (val: PropertySummary | null) => {
  if (val) name.value = val.name
}, { immediate: true })

async function onSubmit() {
  nameError.value = ''
  const trimmed = name.value.trim()
  if (!trimmed) {
    nameError.value = t('validation.required', { field: t('property.name') })
    return
  }
  const ok = await updateProperty(propertyId.value, trimmed)
  if (ok) await navigateTo(`/app/properties/${propertyId.value}`)
}

async function confirmDelete() {
  showDeleteConfirm.value = false
  await deleteProperty(propertyId.value)
  await router.push('/app/properties')
}
</script>

<template>
  <div>
    <div v-if="pending" class="py-16 text-center">
      <p class="text-muted">{{ t('common.loading') }}</p>
    </div>

    <template v-else-if="property">
      <GsBreadcrumb
        :items="[
          { label: t('property.title'), to: '/app/properties' },
          { label: property.name, to: `/app/properties/${propertyId}` },
          { label: t('common.edit') },
        ]"
      />

      <GsPageHeader :title="`${t('common.edit')} ${property.name}`" />

      <UForm :state="{ name }" class="max-w-lg space-y-5" @submit="onSubmit">
        <UFormField :label="t('property.name')" name="name" :error="nameError || undefined" required>
          <UInput v-model="name" :placeholder="t('property.name')" required class="w-full" />
        </UFormField>

        <div class="flex items-center gap-3 pt-2">
          <UButton type="submit" :loading="saving">
            {{ t('common.save') }}
          </UButton>
          <UButton variant="ghost" :to="`/app/properties/${propertyId}`">
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
            {{ t('property.deleteTitle') }}
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
      :title="t('property.deleteTitle')"
      :description="t('property.deleteConfirm')"
      :confirm-label="t('common.delete')"
      danger
      @confirm="confirmDelete"
    />
  </div>
</template>
