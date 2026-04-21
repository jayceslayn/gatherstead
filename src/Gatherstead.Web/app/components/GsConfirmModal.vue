<script setup lang="ts">
const props = defineProps<{
  title: string
  description?: string
  confirmLabel?: string
  danger?: boolean
}>()

const emit = defineEmits<{
  confirm: []
}>()

const open = defineModel<boolean>('open', { default: false })
const { t } = useI18n()

function confirm() {
  emit('confirm')
  open.value = false
}
</script>

<template>
  <UModal v-model:open="open">
    <template #content>
      <div class="p-6">
        <h3 class="text-lg font-semibold mb-2">{{ props.title }}</h3>
        <p v-if="props.description" class="text-sm text-muted mb-6">{{ props.description }}</p>
        <div class="flex justify-end gap-3">
          <UButton variant="ghost" @click="open = false">{{ t('common.cancel') }}</UButton>
          <UButton :color="props.danger ? 'error' : 'primary'" @click="confirm">
            {{ props.confirmLabel ?? t('common.confirm') }}
          </UButton>
        </div>
      </div>
    </template>
  </UModal>
</template>
