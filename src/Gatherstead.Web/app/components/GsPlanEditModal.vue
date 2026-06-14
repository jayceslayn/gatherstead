<script setup lang="ts">
defineProps<{
  title: string
  loading?: boolean
}>()

const open = defineModel<boolean>('open', { default: false })
const notes = defineModel<string>('notes', { default: '' })
const emit = defineEmits<{ submit: [] }>()

const { t } = useI18n()
</script>

<template>
  <UModal v-model:open="open" :title="title">
    <template #body>
      <UFormField :label="t('common.notes')">
        <UTextarea v-model="notes" :rows="3" class="w-full" />
      </UFormField>
    </template>

    <template #footer>
      <div class="flex justify-end gap-3 w-full">
        <UButton variant="ghost" :disabled="loading" @click="open = false">
          {{ t('common.cancel') }}
        </UButton>
        <UButton :loading="loading" @click="emit('submit')">
          {{ t('common.save') }}
        </UButton>
      </div>
    </template>
  </UModal>
</template>
