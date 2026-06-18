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
      <GsFormFooter
        :submit-label="t('common.save')"
        :loading="loading"
        @submit="emit('submit')"
        @cancel="open = false"
      />
    </template>
  </UModal>
</template>
