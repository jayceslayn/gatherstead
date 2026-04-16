<script setup lang="ts">
definePageMeta({
  middleware: 'auth',
})

const { t } = useI18n()
const { tenants, pending, error } = useTenants()
const lastTenantId = useCookie('last_tenant_id')

watch(
  () => tenants.value,
  (list) => {
    if (!list?.length) return
    if (lastTenantId.value && list.some(t => t.id === lastTenantId.value)) {
      navigateTo(`/tenants/${lastTenantId.value}`)
    }
  },
  { immediate: true },
)
</script>

<template>
  <UContainer class="max-w-4xl py-12">
    <h1 class="text-2xl font-bold mb-2">{{ t('tenant.selectTitle') }}</h1>
    <p class="text-gray-500 dark:text-gray-400 mb-8">{{ t('tenant.selectDescription') }}</p>

    <div v-if="pending" class="text-center py-12">
      <p class="text-gray-500">{{ t('common.loading') }}</p>
    </div>

    <div v-else-if="error" class="text-center py-12">
      <p class="text-red-500">{{ t('error.fetchFailed') }}</p>
    </div>

    <div v-else-if="!tenants?.length" class="text-center py-12">
      <p class="text-gray-500 mb-2">{{ t('tenant.noTenants') }}</p>
      <p class="text-gray-400 text-sm">{{ t('tenant.noTenantsHint') }}</p>
    </div>

    <div v-else class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
      <UCard
        v-for="tenant in tenants"
        :key="tenant.id"
        class="cursor-pointer hover:ring-2 hover:ring-primary-500 transition-shadow"
        @click="navigateTo(`/tenants/${tenant.id}`)"
      >
        <h3 class="font-semibold">{{ tenant.name }}</h3>
      </UCard>
    </div>
  </UContainer>
</template>
