<script setup lang="ts">
const model = defineModel<string>({ default: '' })
defineProps<{ placeholder?: string }>()
const { t } = useI18n()
</script>

<template>
  <UInput
    v-model="model"
    icon="i-heroicons-magnifying-glass"
    :placeholder="placeholder"
    class="w-full sm:max-w-xs"
  >
    <template v-if="model" #trailing>
      <UButton
        color="neutral"
        variant="link"
        size="xs"
        icon="i-heroicons-x-mark-20-solid"
        :aria-label="t('common.clear')"
        @click="model = ''"
      />
    </template>
  </UInput>
</template>
