<script setup lang="ts">
definePageMeta({
  layout: 'landing',
})

const { t } = useI18n()
const { loggedIn } = useAuth()

const features = computed(() => [
  { title: t('landing.featureHouseholds'), description: t('landing.featureHouseholdsDesc'), icon: 'i-heroicons-home' },
  { title: t('landing.featureEvents'), description: t('landing.featureEventsDesc'), icon: 'i-heroicons-calendar-days' },
  { title: t('landing.featureLodging'), description: t('landing.featureLodgingDesc'), icon: 'i-heroicons-building-office' },
  { title: t('landing.featureDirectory'), description: t('landing.featureDirectoryDesc'), icon: 'i-heroicons-user-group' },
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
        <UButton
          v-if="loggedIn"
          to="/tenants"
          size="lg"
          color="primary"
        >
          {{ t('common.getStarted') }}
        </UButton>
        <UButton
          v-else
          to="/auth/azure"
          external
          size="lg"
          color="primary"
          variant="solid"
          icon="i-heroicons-arrow-left-end-on-rectangle"
        >
          {{ t('common.signIn') }}
        </UButton>
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
