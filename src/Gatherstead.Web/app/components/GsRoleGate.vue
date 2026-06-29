<script setup lang="ts">
import { useTenantRole } from '~/composables/useTenantRole'
import type { TenantRole } from '~/repositories/types'

const props = defineProps<{
  minRole: TenantRole
}>()

const { role, isAppAdmin } = useTenantRole()

const roleOrder: Record<TenantRole, number> = {
  Guest: 0,
  Member: 1,
  Coordinator: 2,
  Manager: 3,
  Owner: 4,
}

const hasAccess = computed(() => {
  // App admins clear every threshold (authority axis); role stays null so PII remains masked.
  if (isAppAdmin.value) return true
  if (!role.value) return false
  return roleOrder[role.value] >= roleOrder[props.minRole]
})
</script>

<template>
  <slot v-if="hasAccess" />
</template>
