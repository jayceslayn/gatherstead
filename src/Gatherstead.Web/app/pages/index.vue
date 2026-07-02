<script setup lang="ts">
definePageMeta({
  layout: 'landing',
})

const { t } = useI18n()
const { loggedIn, login } = useAuth()
const config = useRuntimeConfig()
const isDemoMode = __DEMO_MODE__

// Persist a spinner on the clicked CTA until the browser navigates away.
const signingIn = ref(false)
function onGetStarted() {
  signingIn.value = true
  navigateTo('/tenants')
}
function onSignIn() {
  signingIn.value = true
  login()
}

const features = computed(() => [
  { title: t('landing.featureDirectory'), description: t('landing.featureDirectoryDesc'), icon: 'i-heroicons-user-group' },
  { title: t('landing.featureProperties'), description: t('landing.featurePropertiesDesc'), icon: 'i-heroicons-map-pin' },
  { title: t('landing.featureEvents'), description: t('landing.featureEventsDesc'), icon: 'i-heroicons-calendar-days' },
  { title: t('landing.featureAccommodation'), description: t('landing.featureAccommodationDesc'), icon: 'i-heroicons-building-office' },
])
</script>

<template>
  <div>
    <section class="py-20 text-center">
      <UContainer class="max-w-3xl">
        <h1 class="text-4xl sm:text-5xl font-bold mb-4">
          {{ t('landing.heroTitle') }}
        </h1>
        <p class="text-xl text-neutral-600 dark:text-neutral-400 mb-2">
          {{ t('landing.heroSubtitle') }}
        </p>
        <p class="text-neutral-500 dark:text-neutral-400 mb-8 max-w-2xl mx-auto">
          {{ t('landing.heroDescription') }}
        </p>
        <div class="flex flex-wrap items-center justify-center gap-3">
          <UButton
            v-if="!isDemoMode && config.public.demoUrl"
            :to="(config.public.demoUrl as string)"
            external
            size="lg"
            color="primary"
            variant="soft"
          >
            {{ t('landing.tryDemo') }}
          </UButton>
          <UButton
            v-if="isDemoMode && config.public.liveUrl"
            :to="(config.public.liveUrl as string)"
            external
            size="lg"
            color="primary"
            variant="soft"
          >
            {{ t('landing.visitLiveSite') }}
          </UButton>

          <UButton
            v-if="loggedIn"
            size="lg"
            color="primary"
            :loading="signingIn"
            @click="onGetStarted"
          >
            {{ t('common.getStarted') }}
          </UButton>
          <UButton
            v-else
            size="lg"
            color="primary"
            variant="solid"
            icon="i-heroicons-arrow-left-end-on-rectangle"
            :loading="signingIn"
            @click="onSignIn"
          >
            {{ t('common.signIn') }}
          </UButton>
        </div>
      </UContainer>
    </section>

    <section class="py-16 bg-neutral-50 dark:bg-neutral-900">
      <UContainer>
        <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-8">
          <div
            v-for="feature in features"
            :key="feature.title"
            class="bg-white dark:bg-neutral-800 rounded-lg p-6 shadow-sm"
          >
            <div class="text-primary-500 mb-4">
              <UIcon :name="feature.icon" class="text-3xl" />
            </div>
            <h3 class="font-semibold text-lg mb-2">{{ feature.title }}</h3>
            <p class="text-neutral-500 dark:text-neutral-400 text-sm">{{ feature.description }}</p>
          </div>
        </div>
      </UContainer>
    </section>
  </div>
</template>
