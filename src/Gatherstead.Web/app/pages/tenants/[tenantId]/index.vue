<script setup lang="ts">
const route = useRoute()
const lastTenantId = useCookie('last_tenant_id')

onMounted(() => {
  lastTenantId.value = route.params.tenantId as string
  navigateTo('/app', { replace: true })
})
</script>

<template>
  <div />
</template>
