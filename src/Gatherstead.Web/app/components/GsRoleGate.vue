<script setup lang="ts">
import { useTenantRole } from '~/composables/useTenantRole'
import type { TenantRole } from '~/repositories/types'
import { hasMinimumTenantRole } from '~/utils/tenantRoles'

const props = defineProps<{
  minRole: TenantRole
}>()

const { role, isAppAdmin } = useTenantRole()

const hasAccess = computed(() => hasMinimumTenantRole(role.value, props.minRole, isAppAdmin.value))
</script>

<template>
  <slot v-if="hasAccess" />
</template>
