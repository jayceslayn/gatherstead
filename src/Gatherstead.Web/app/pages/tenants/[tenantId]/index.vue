<script setup lang="ts">
definePageMeta({
  middleware: 'auth',
  layout: 'default',
})

const { t } = useI18n()
const route = useRoute()
const lastTenantId = useCookie('last_tenant_id')

onMounted(() => {
  lastTenantId.value = route.params.tenantId as string
})
</script>

<template>
  <div>
    <h1 class="text-2xl font-bold mb-4">{{ t('tenant.dashboard') }}</h1>
    <p class="text-gray-500">{{ t('tenant.dashboardPlaceholder') }}</p>
  </div>
</template>
