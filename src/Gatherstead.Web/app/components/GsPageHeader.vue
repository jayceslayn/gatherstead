<script setup lang="ts">
defineProps<{
  title: string
  description?: string
}>()
</script>

<template>
  <div class="flex items-start justify-between mb-6">
    <div class="min-w-0">
      <h1 class="text-2xl font-bold tracking-tight truncate">{{ title }}</h1>
      <p v-if="description" class="text-sm text-muted mt-1">{{ description }}</p>
    </div>
    <div class="flex items-center gap-2 ml-4 shrink-0">
      <slot />
    </div>
  </div>
</template>
