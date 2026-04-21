<script setup lang="ts">
import { useTenantStore } from '~/stores/tenant'

const props = defineProps<{
  items: Array<{ label: string; to?: string }>
}>()

const tenantStore = useTenantStore()

const allItems = computed(() => [
  { label: tenantStore.currentTenantName ?? '…', to: '/app' },
  ...props.items,
])
</script>

<template>
  <UBreadcrumb :items="allItems" class="mb-4 text-sm" />
</template>
