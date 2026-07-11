<script setup lang="ts">
import { useDismissibleHint } from '~/composables/useDismissibleHint'

const props = withDefaults(defineProps<{
  storageKey: string
  title: string
  description: string
  version?: number
  icon?: string
}>(), {
  version: 1,
  icon: 'i-heroicons-cursor-arrow-rays',
})

const { t } = useI18n()
const { visible, dismiss, show } = useDismissibleHint(props.storageKey, props.version)
</script>

<template>
  <UAlert
    v-if="visible"
    :icon="icon"
    color="info"
    variant="soft"
    :title="title"
    :description="description"
    close
    @update:open="dismiss()"
  />
  <div v-else class="flex justify-end">
    <UButton
      icon="i-heroicons-question-mark-circle"
      variant="ghost"
      color="neutral"
      size="xs"
      :aria-label="t('common.hint.show')"
      @click="show()"
    />
  </div>
</template>
