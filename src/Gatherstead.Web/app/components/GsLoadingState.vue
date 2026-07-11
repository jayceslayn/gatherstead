<script setup lang="ts">
// Shared in-flight placeholder, mirroring GsEmptyState's shape. Defaults to the
// common.loading label; pass `label` for context-specific copy.
defineProps<{
  label?: string
}>()

const { t } = useI18n()
</script>

<template>
  <div class="flex flex-col items-center justify-center py-16 text-center">
    <UIcon name="i-heroicons-arrow-path" class="size-6 text-muted animate-spin mb-3" />
    <p class="text-muted">{{ label ?? t('common.loading') }}</p>
  </div>
</template>
