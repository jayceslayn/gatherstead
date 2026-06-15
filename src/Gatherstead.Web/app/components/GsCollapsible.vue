<script setup lang="ts">
const open = defineModel<boolean>('open', { default: false })
defineProps<{ buttonClass?: string }>()

const { t } = useI18n()
</script>

<template>
  <div>
    <button
      type="button"
      class="w-full flex items-center justify-between gap-2"
      :class="buttonClass"
      :aria-expanded="open"
      :aria-label="open ? t('collapsible.hide') : t('collapsible.show')"
      @click="open = !open"
    >
      <slot />
      <UIcon
        name="i-heroicons-chevron-down"
        class="size-4 shrink-0 transition-transform"
        :class="open ? 'rotate-180' : ''"
      />
    </button>
    <div v-show="open">
      <slot name="content" />
    </div>
  </div>
</template>
