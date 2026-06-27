<script setup lang="ts">
definePageMeta({
  layout: 'landing',
})

const { t } = useI18n()
const config = useRuntimeConfig()

const contactEmail = computed(() => config.public.contactEmail as string)
const githubUrl = computed(() => config.public.githubUrl as string)
const docsUrl = computed(() => config.public.docsUrl as string)

const topics = computed(() => [
  { icon: 'i-heroicons-user-group', text: t('contact.topicGroups') },
  { icon: 'i-heroicons-credit-card', text: t('contact.topicSubscription') },
  { icon: 'i-heroicons-wrench-screwdriver', text: t('contact.topicTechnical') },
])
</script>

<template>
  <UContainer class="max-w-2xl py-16">
    <h1 class="text-3xl font-bold mb-3">{{ t('contact.title') }}</h1>

    <section class="mb-10">
      <h2 class="font-semibold text-lg mb-3">{{ t('contact.topicsHeading') }}</h2>
      <ul class="space-y-2">
        <li
          v-for="topic in topics"
          :key="topic.text"
          class="flex items-start gap-3 text-neutral-600 dark:text-neutral-400"
        >
          <UIcon :name="topic.icon" class="text-primary-500 text-xl shrink-0 mt-0.5" />
          <span>{{ topic.text }}</span>
        </li>
      </ul>
    </section>

    <section class="mb-10">
      <h2 class="font-semibold text-lg mb-3">{{ t('contact.emailHeading') }}</h2>
      <template v-if="contactEmail">
        <p class="text-neutral-600 dark:text-neutral-400 mb-3">{{ t('contact.emailIntro') }}</p>
        <UButton
          :to="`mailto:${contactEmail}`"
          external
          color="primary"
          icon="i-heroicons-envelope"
        >
          {{ contactEmail }}
        </UButton>
      </template>
      <p v-else class="text-neutral-500 dark:text-neutral-400">{{ t('contact.emailFallback') }}</p>
    </section>

    <section v-if="docsUrl || githubUrl">
      <h2 class="font-semibold text-lg mb-3">{{ t('contact.resourcesHeading') }}</h2>
      <div class="flex flex-wrap gap-3">
        <UButton
          v-if="docsUrl"
          :to="docsUrl"
          target="_blank"
          variant="soft"
          color="neutral"
          icon="i-heroicons-book-open"
        >
          {{ t('contact.viewDocs') }}
        </UButton>
        <UButton
          v-if="githubUrl"
          :to="githubUrl"
          target="_blank"
          variant="soft"
          color="neutral"
          icon="i-heroicons-code-bracket"
        >
          {{ t('contact.viewGithub') }}
        </UButton>
      </div>
    </section>
  </UContainer>
</template>
