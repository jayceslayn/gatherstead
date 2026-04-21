<script setup lang="ts">
import { useTenantRole } from '~/composables/useTenantRole'
import type { TenantRole } from '~/composables/useTenants'

const props = defineProps<{
  minRole: TenantRole
}>()

const { role } = useTenantRole()

const roleOrder: Record<TenantRole, number> = {
  Guest: 0,
  Member: 1,
  Manager: 2,
  Owner: 3,
}

const hasAccess = computed(() => {
  if (!role.value) return false
  return roleOrder[role.value] >= roleOrder[props.minRole]
})
</script>

<template>
  <slot v-if="hasAccess" />
</template>
