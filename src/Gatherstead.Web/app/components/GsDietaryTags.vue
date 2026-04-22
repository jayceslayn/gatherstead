<script setup lang="ts">
defineProps<{
  allergies?: string[]
  restrictions?: string[]
  dietaryTags?: string[]
}>()
</script>

<template>
  <div class="flex flex-wrap gap-1.5">
    <UBadge
      v-for="tag in (allergies ?? [])"
      :key="`a-${tag}`"
      color="secondary"
      variant="subtle"
      size="sm"
    >
      {{ tag }}
    </UBadge>
    <UBadge
      v-for="tag in (restrictions ?? [])"
      :key="`r-${tag}`"
      color="success"
      variant="subtle"
      size="sm"
    >
      {{ tag }}
    </UBadge>
    <UBadge
      v-for="tag in (dietaryTags ?? [])"
      :key="`d-${tag}`"
      color="neutral"
      variant="subtle"
      size="sm"
    >
      {{ tag }}
    </UBadge>
  </div>
</template>
