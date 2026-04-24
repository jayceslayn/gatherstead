<script setup lang="ts">
const route = useRoute()
const { switchToTenantId } = useTenantSelect()

onMounted(() => {
  switchToTenantId(route.params.tenantId as string)
})
</script>

<template>
  <div />
</template>
