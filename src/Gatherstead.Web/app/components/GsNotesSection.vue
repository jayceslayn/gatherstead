<script setup lang="ts">
defineProps<{ notes: string | null | undefined }>()

const { t } = useI18n()
</script>

<template>
  <GsCollapsible
    v-if="notes"
    :open="true"
    button-class="text-sm font-semibold text-muted uppercase tracking-wide"
  >
    {{ t('notes.title') }}
    <template #content>
      <p class="text-sm mt-2 whitespace-pre-wrap break-words">{{ notes }}</p>
    </template>
  </GsCollapsible>
</template>
