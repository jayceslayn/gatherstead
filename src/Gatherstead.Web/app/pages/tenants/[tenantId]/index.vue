<script setup lang="ts">
definePageMeta({
  middleware: 'auth',
  layout: 'landing',
})

const route = useRoute()
const { switchToTenantId } = useTenantSelect()

onMounted(() => {
  switchToTenantId(route.params.tenantId as string)
})
</script>

<template>
  <div />
</template>
