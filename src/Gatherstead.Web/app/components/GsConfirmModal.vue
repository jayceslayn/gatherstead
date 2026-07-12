<script setup lang="ts">
const props = defineProps<{
  title: string
  description?: string
  confirmLabel?: string
  danger?: boolean
  /** Shows a spinner on the confirm button and disables cancel while an async action runs. */
  loading?: boolean
  /** Disables the confirm button (e.g. until a type-to-confirm phrase matches). */
  confirmDisabled?: boolean
  /** Set false when the caller closes the modal itself only after the action succeeds. */
  closeOnConfirm?: boolean
}>()

const emit = defineEmits<{
  confirm: []
}>()

const open = defineModel<boolean>('open', { default: false })
const { t } = useI18n()

function confirm() {
  emit('confirm')
  if (props.closeOnConfirm !== false)
    open.value = false
}
</script>

<template>
  <UModal v-model:open="open">
    <template #content>
      <div class="p-6">
        <h3 class="text-lg font-semibold mb-2">{{ props.title }}</h3>
        <p v-if="props.description" class="text-sm text-muted mb-6">{{ props.description }}</p>
        <slot />
        <div class="flex justify-end gap-3">
          <UButton variant="ghost" :disabled="props.loading" @click="open = false">{{ t('common.cancel') }}</UButton>
          <UButton
            :color="props.danger ? 'error' : 'primary'"
            :loading="props.loading"
            :disabled="props.confirmDisabled"
            @click="confirm"
          >
            {{ props.confirmLabel ?? t('common.confirm') }}
          </UButton>
        </div>
      </div>
    </template>
  </UModal>
</template>
