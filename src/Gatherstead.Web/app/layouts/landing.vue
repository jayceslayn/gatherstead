<script setup lang="ts">
const { t } = useI18n()
const { loggedIn, logout, isDemo } = useAuth()
const config = useRuntimeConfig()
</script>

<!-- eslint-disable vue/no-multiple-template-root -->
<template>
  <DemoBanner />
  <UHeader to="/" :toggle="!isDemo">
    <template #title>
      <picture>
        <source media="(min-width: 640px)" srcset="/images/gatherstead_logo_full_wide.png">
        <NuxtImg src="/images/gatherstead_logo_small.png" :alt="t('common.appName')" class="h-10 w-auto" />
      </picture>
    </template>
    <template #right>
      <LocaleSwitcher />
      <UButton
        v-if="!isDemo && config.public.demoUrl"
        variant="soft"
        :to="(config.public.demoUrl as string)"
        external
      >
        {{ t('landing.tryDemo') }}
      </UButton>
      <template v-if="!isDemo">
        <UButton
          v-if="loggedIn"
          variant="ghost"
          to="/tenants"
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
      </template>
    </template>
  </UHeader>

  <UMain>
    <slot />
  </UMain>

  <UFooter>
    <div class="flex items-center gap-6 text-sm text-(--ui-text-muted)">
      <UButton
        v-if="config.public.githubUrl"
        :to="config.public.githubUrl as string"
        target="_blank"
        variant="link"
        color="neutral"
      >
        {{ t('landing.viewOnGithub') }}
      </UButton>
      <UButton
        v-if="config.public.docsUrl"
        :to="config.public.docsUrl as string"
        target="_blank"
        variant="link"
        color="neutral"
      >
        {{ t('landing.viewDocs') }}
      </UButton>
    </div>
  </UFooter>
</template>
