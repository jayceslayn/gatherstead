<script setup lang="ts">
const { t } = useI18n()
const { logout } = useAuth()
</script>

<template>
  <div class="min-h-screen flex">
    <aside class="w-64 border-r border-gray-200 dark:border-gray-800 p-4 flex flex-col">
      <NuxtLink to="/" class="text-xl font-bold mb-8">
        {{ t('common.appName') }}
      </NuxtLink>
      <nav class="flex flex-col gap-2 flex-1">
        <UButton variant="ghost" to="/tenants" class="justify-start">
          {{ t('nav.tenants') }}
        </UButton>
      </nav>
      <UButton variant="soft" @click="() => { logout() }" class="mt-auto">
        {{ t('common.signOut') }}
      </UButton>
    </aside>
    <main class="flex-1 p-6">
      <slot />
    </main>
  </div>
</template>
