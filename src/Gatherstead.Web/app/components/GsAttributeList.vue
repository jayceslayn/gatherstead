<script setup lang="ts">
import type { AttributeEntry } from '~/repositories/types'
import { useAttributeRoles } from '~/composables/useAttributeRoles'

defineProps<{ attributes: AttributeEntry[] }>()

const { roleLabel } = useAttributeRoles()
</script>

<template>
  <dl v-if="attributes.length" class="space-y-3">
    <div
      v-for="attr in attributes"
      :key="attr.id"
      class="text-sm"
    >
      <div class="flex items-baseline gap-2">
        <dt class="font-medium text-muted break-words min-w-0">{{ attr.key }}</dt>
        <UBadge variant="subtle" size="sm" color="neutral" class="ml-auto shrink-0">
          {{ roleLabel(attr.tenantMinRole) }}
        </UBadge>
      </div>
      <dd class="mt-0.5 break-words whitespace-pre-wrap">{{ attr.value }}</dd>
    </div>
  </dl>
</template>
