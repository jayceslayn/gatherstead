<script setup lang="ts">
import { usePropertyActions } from '~/composables/useProperties'

const props = defineProps<{
  refresh: () => Promise<void>
}>()

const open = defineModel<boolean>('open', { default: false })
const { t } = useI18n()

const { updating, createProperty } = usePropertyActions(props.refresh)

const name = ref('')
const nameError = ref('')
const saving = computed(() => updating.value.includes('new'))

watch(open, (isOpen) => {
  if (!isOpen) {
    name.value = ''
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
  const success = await createProperty(trimmed)
  if (success) open.value = false
}
</script>

<template>
  <UModal v-model:open="open">
    <template #content>
      <div class="p-6">
        <h3 class="text-lg font-semibold mb-4">{{ t('property.createTitle') }}</h3>
        <UFormField :label="t('property.name')" :error="nameError || undefined">
          <UInput
            v-model="name"
            :placeholder="t('property.name')"
            @keydown.enter="submit"
          />
        </UFormField>
        <div class="flex justify-end gap-3 mt-6">
          <UButton variant="ghost" :disabled="saving" @click="open = false">
            {{ t('common.cancel') }}
          </UButton>
          <UButton :loading="saving" @click="submit">
            {{ t('common.create') }}
          </UButton>
        </div>
      </div>
    </template>
  </UModal>
</template>
