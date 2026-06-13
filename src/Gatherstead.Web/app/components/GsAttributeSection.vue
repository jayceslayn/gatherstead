<script setup lang="ts">
import type { AttributeEntry } from '~/repositories/types'

defineProps<{ attributes: AttributeEntry[] }>()

const { t } = useI18n()
const open = ref(false)
</script>

<template>
  <div v-if="attributes.length">
    <button
      type="button"
      class="w-full flex items-center justify-between gap-2 text-sm font-semibold text-muted uppercase tracking-wide"
      :aria-expanded="open"
      :aria-label="open ? t('attribute.hideDetails') : t('attribute.showDetails')"
      @click="open = !open"
    >
      <span>{{ t('attribute.title') }}</span>
      <UIcon
        name="i-heroicons-chevron-down"
        class="size-4 shrink-0 transition-transform"
        :class="open ? 'rotate-180' : ''"
      />
    </button>
    <GsAttributeList v-show="open" :attributes="attributes" class="mt-2" />
  </div>
</template>
