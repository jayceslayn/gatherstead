<script setup lang="ts">
const { t } = useI18n()
const { logout } = useAuth()

const items = computed(() => [
  [{ label: t('nav.tenants'), icon: 'i-heroicons-user-group', to: '/tenants' }],
])
</script>

<template>
  <div class="min-h-screen flex">
    <aside class="w-64 border-r border-(--ui-border) p-4 flex flex-col">
      <NuxtLink to="/" class="mb-8">
        <picture>
          <source media="(min-width: 640px)" srcset="/images/gatherstead_logo_full.png" />
          <NuxtImg src="/images/gatherstead_logo_small.png" :alt="t('common.appName')" class="h-12 w-auto" />
        </picture>
      </NuxtLink>
      <UNavigationMenu orientation="vertical" :items="items" highlight class="flex-1" />
      <LocaleSwitcher class="mt-auto mb-2" />
      <UButton variant="soft" @click="() => { logout() }">
        {{ t('common.signOut') }}
      </UButton>
    </aside>
    <UMain class="flex-1 p-6">
      <slot />
    </UMain>
  </div>
</template>
