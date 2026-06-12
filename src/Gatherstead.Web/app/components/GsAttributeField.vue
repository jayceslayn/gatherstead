<script setup lang="ts">
import type { AttributeWriteEntry } from '~/repositories/types'

// Drop-in form field: a labelled "Additional Details" custom-attribute editor.
// Use in any entity create/edit form that supports attributes.
const model = defineModel<AttributeWriteEntry[]>({ required: true })
const { t } = useI18n()
</script>

<template>
  <UFormField :label="t('attribute.title')" :hint="t('attribute.hint')">
    <GsAttributeEditor v-model="model" />
  </UFormField>
</template>
