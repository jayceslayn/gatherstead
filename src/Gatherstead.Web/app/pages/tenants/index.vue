<script setup lang="ts">
definePageMeta({
  middleware: 'auth',
  layout: 'landing',
})

const { t } = useI18n()
const { tenants, pending, error } = useTenants()
const { selectTenant } = useTenantSelect()
// While a silent re-auth redirect is in flight, a 401 sets `error` but the app is recovering, not
// failing — suppress the terminal error so it doesn't flash (the global ReauthBanner is the signal).
const reauthing = useReauth()
</script>

<template>
  <UContainer class="max-w-4xl py-12">
    <h1 class="text-2xl font-bold mb-2">{{ t('tenant.selectTitle') }}</h1>
    <p class="text-gray-500 dark:text-gray-400 mb-8">{{ t('tenant.selectDescription') }}</p>

    <div v-if="pending || reauthing" class="text-center py-12">
      <p class="text-gray-500">{{ t('common.loading') }}</p>
    </div>

    <div v-else-if="error" class="text-center py-12">
      <p class="text-red-500">{{ t('error.fetchFailed') }}</p>
    </div>

    <div v-else-if="!tenants?.length" class="text-center py-12">
      <p class="text-gray-500 mb-2">{{ t('tenant.noTenants') }}</p>
      <p class="text-gray-400 text-sm mb-6">{{ t('tenant.noTenantsHint') }}</p>
      <UButton
        to="/contact"
        variant="soft"
        color="primary"
        icon="i-heroicons-envelope"
      >
        {{ t('tenant.contactCta') }}
      </UButton>
    </div>

    <div v-else class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
      <UCard
        v-for="tenant in tenants"
        :key="tenant.id"
        class="cursor-pointer hover:ring-2 hover:ring-primary-500 transition-shadow"
        @click="selectTenant(tenant)"
      >
        <h3 class="font-semibold">{{ tenant.name }}</h3>
      </UCard>
    </div>
  </UContainer>
</template>
