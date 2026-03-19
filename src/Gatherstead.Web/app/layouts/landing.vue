<script setup lang="ts">
const { t } = useI18n()
const { loggedIn, logout } = useAuth()
const config = useRuntimeConfig()
</script>

<template>
  <div class="min-h-screen flex flex-col">
    <header class="border-b border-gray-200 dark:border-gray-800">
      <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 h-16 flex items-center justify-between">
        <NuxtLink to="/" class="text-xl font-bold">
          {{ t('common.appName') }}
        </NuxtLink>
        <nav class="flex items-center gap-4">
          <UButton
            v-if="loggedIn"
            variant="ghost"
            :to="'/tenants'"
          >
            {{ t('nav.tenants') }}
          </UButton>
          <UButton
            v-if="loggedIn"
            variant="soft"
            @click="() => { logout() }"
          >
            {{ t('common.signOut') }}
          </UButton>
          <UButton
            v-else
            color="primary"
            to="/auth/azure"
            external
          >
            {{ t('common.signIn') }}
          </UButton>
        </nav>
      </div>
    </header>

    <main class="flex-1">
      <slot />
    </main>

    <footer class="border-t border-gray-200 dark:border-gray-800 py-8">
      <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 flex items-center justify-center gap-6 text-sm text-gray-500">
        <a
          v-if="config.public.githubUrl"
          :href="config.public.githubUrl as string"
          target="_blank"
          rel="noopener noreferrer"
          class="hover:text-gray-700 dark:hover:text-gray-300"
        >
          {{ t('landing.viewOnGithub') }}
        </a>
        <a
          v-if="config.public.docsUrl"
          :href="config.public.docsUrl as string"
          target="_blank"
          rel="noopener noreferrer"
          class="hover:text-gray-700 dark:hover:text-gray-300"
        >
          {{ t('landing.viewDocs') }}
        </a>
      </div>
    </footer>
  </div>
</template>
