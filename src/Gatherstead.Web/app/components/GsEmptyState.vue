<script setup lang="ts">
defineProps<{
  icon?: string
  title: string
  description?: string
}>()
</script>

<template>
  <div class="flex flex-col items-center justify-center py-16 text-center">
    <UIcon v-if="icon" :name="icon" class="size-12 text-muted mb-4" />
    <h3 class="text-lg font-semibold mb-1">{{ title }}</h3>
    <p v-if="description" class="text-sm text-muted mb-6 max-w-sm">{{ description }}</p>
    <slot />
  </div>
</template>
