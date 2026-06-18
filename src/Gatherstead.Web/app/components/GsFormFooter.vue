<script setup lang="ts">
const props = defineProps<{
  submitLabel: string
  loading?: boolean
  disabled?: boolean
  cancelTo?: string
  cancelLabel?: string
  submitType?: 'button' | 'submit'
}>()

const emit = defineEmits<{
  submit: []
  cancel: []
}>()

const { t } = useI18n()
const resolvedCancelLabel = computed(() => props.cancelLabel ?? t('common.cancel'))
</script>

<template>
  <div class="flex items-center justify-between gap-3 w-full pt-2">
    <div>
      <slot name="delete" />
    </div>
    <div class="flex items-center gap-3">
      <UButton
        v-if="cancelTo"
        variant="ghost"
        :to="cancelTo"
      >
        {{ resolvedCancelLabel }}
      </UButton>
      <UButton
        v-else
        variant="ghost"
        @click="emit('cancel')"
      >
        {{ resolvedCancelLabel }}
      </UButton>
      <UButton
        :type="submitType === 'submit' ? 'submit' : 'button'"
        :loading="loading"
        :disabled="disabled"
        @click="submitType !== 'submit' ? emit('submit') : undefined"
      >
        {{ submitLabel }}
      </UButton>
    </div>
  </div>
</template>
