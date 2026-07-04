<script setup lang="ts">
const { t } = useI18n()
const reauthInFlight = useReauth()
</script>

<template>
  <div
    v-if="reauthInFlight"
    class="fixed top-0 inset-x-0 z-[60] bg-primary-50 dark:bg-primary-950 border-b border-primary-200 dark:border-primary-800"
  >
    <div class="max-w-screen-xl mx-auto px-4 py-1.5 flex items-center justify-center gap-2">
      <UIcon name="i-heroicons-arrow-path" class="size-4 text-primary-600 dark:text-primary-400 shrink-0 animate-spin" />
      <span class="text-sm font-medium text-primary-900 dark:text-primary-100">{{ t('auth.reauthenticating') }}</span>
    </div>
  </div>
</template>
