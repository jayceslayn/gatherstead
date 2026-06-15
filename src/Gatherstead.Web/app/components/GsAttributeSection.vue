<script setup lang="ts">
import type { AttributeEntry } from '~/repositories/types'

defineProps<{ attributes: AttributeEntry[] }>()

const { t } = useI18n()
</script>

<template>
  <GsCollapsible
    v-if="attributes.length"
    button-class="text-sm font-semibold text-muted uppercase tracking-wide"
  >
    {{ t('attribute.title') }}
    <template #content>
      <GsAttributeList :attributes="attributes" class="mt-2" />
    </template>
  </GsCollapsible>
</template>
