<script setup lang="ts">
import { usePropertyActions } from '~/composables/useProperties'
import type { PropertySummary } from '~/repositories/types'

// Create + edit in one component. Passing `property` switches to edit mode; omitting it creates.
const props = defineProps<{
  refresh: () => Promise<void>
  property?: PropertySummary | null
}>()

const open = defineModel<boolean>('open', { default: false })
const { t } = useI18n()

const { updating, createProperty, updateProperty } = usePropertyActions(props.refresh)

const isEdit = computed(() => !!props.property)
const name = ref('')
const nameError = ref('')
const saving = computed(() => updating.value.includes(props.property?.id ?? 'new'))

watch(open, (isOpen) => {
  if (isOpen) {
    name.value = props.property?.name ?? ''
    nameError.value = ''
  }
})

async function submit() {
  nameError.value = ''
  const trimmed = name.value.trim()
  if (!trimmed) {
    nameError.value = t('validation.required', { field: t('property.name') })
    return
  }
  const ok = (isEdit.value && props.property)
    ? await updateProperty(props.property.id, trimmed)
    : await createProperty(trimmed)
  if (ok) open.value = false
}
</script>

<template>
  <UModal v-model:open="open">
    <template #content>
      <div class="p-6">
        <h3 class="text-lg font-semibold mb-4">
          {{ isEdit ? t('property.editTitle') : t('property.createTitle') }}
        </h3>
        <UFormField :label="t('property.name')" :error="nameError || undefined">
          <UInput
            v-model="name"
            :placeholder="t('property.name')"
            @keydown.enter="submit"
            @input="nameError = ''"
          />
        </UFormField>
        <div class="flex justify-end gap-3 mt-6">
          <UButton variant="ghost" :disabled="saving" @click="open = false">
            {{ t('common.cancel') }}
          </UButton>
          <UButton :loading="saving" @click="submit">
            {{ isEdit ? t('common.save') : t('common.create') }}
          </UButton>
        </div>
      </div>
    </template>
  </UModal>
</template>
