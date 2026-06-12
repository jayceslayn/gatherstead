<script setup lang="ts">
import type { AttributeWriteEntry } from '~/repositories/types'
import { useAttributeRoles, isIncompleteAttributeRow, TENANT_ROLE_VALUES } from '~/composables/useAttributeRoles'

// Key/Value lengths mirror the backend column limits (see *Attribute entities).
const KEY_MAX = 50
const VALUE_MAX = 255

const model = defineModel<AttributeWriteEntry[]>({ required: true })
const { t } = useI18n()
const { roleItems } = useAttributeRoles()

function addRow() {
  model.value = [...model.value, { key: '', value: '', tenantMinRole: TENANT_ROLE_VALUES.Member }]
}
function removeRow(index: number) {
  model.value = model.value.filter((_, i) => i !== index)
}
function update(index: number, patch: Partial<AttributeWriteEntry>) {
  model.value = model.value.map((row, i) => (i === index ? { ...row, ...patch } : row))
}
</script>

<template>
  <div class="space-y-3">
    <div
      v-for="(row, i) in model"
      :key="i"
      class="rounded-lg border border-default p-3 space-y-2"
    >
      <div class="flex items-start gap-2">
        <div class="flex-1 min-w-0">
          <UInput
            :model-value="row.key"
            :placeholder="t('attribute.keyPlaceholder')"
            :maxlength="KEY_MAX"
            :color="isIncompleteAttributeRow(row) ? 'error' : undefined"
            :highlight="isIncompleteAttributeRow(row) || undefined"
            class="w-full"
            @update:model-value="update(i, { key: $event as string })"
          />
          <p v-if="isIncompleteAttributeRow(row)" class="text-sm text-error mt-1">
            {{ t('attribute.keyRequired') }}
          </p>
        </div>
        <UButton
          color="error"
          variant="ghost"
          icon="i-heroicons-trash"
          :aria-label="t('common.delete')"
          @click="removeRow(i)"
        />
      </div>
      <UTextarea
        :model-value="row.value"
        :placeholder="t('attribute.valuePlaceholder')"
        :maxlength="VALUE_MAX"
        :rows="1"
        autoresize
        class="w-full"
        @update:model-value="update(i, { value: $event as string })"
      />
      <div class="flex items-center gap-2">
        <UIcon name="i-heroicons-eye" class="size-4 text-muted shrink-0" />
        <USelect
          :model-value="row.tenantMinRole"
          :items="roleItems"
          class="w-full sm:w-56"
          @update:model-value="update(i, { tenantMinRole: $event as number })"
        />
      </div>
    </div>
    <UButton variant="ghost" size="xs" icon="i-heroicons-plus" @click="addRow">
      {{ t('attribute.add') }}
    </UButton>
  </div>
</template>
