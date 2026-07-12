<script setup lang="ts">
import type { LegalDoc } from '~/content/legal/types'

const props = defineProps<{ doc: LegalDoc }>()

const { t } = useI18n()

// Built in script (not the template) so the "·" separator is not a raw-text node (no-raw-text lint rule).
const metaLine = computed(() =>
  `${t('legal.effectiveDate', { date: props.doc.effectiveDate })} · ${t('legal.englishGoverns')}`)
</script>

<template>
  <UContainer class="max-w-2xl py-16">
    <h1 class="text-3xl font-bold mb-2">{{ doc.title }}</h1>
    <p class="text-sm text-neutral-500 dark:text-neutral-400 mb-8">{{ metaLine }}</p>

    <p class="text-neutral-600 dark:text-neutral-400 mb-10">{{ doc.intro }}</p>

    <section
      v-for="section in doc.sections"
      :id="section.id"
      :key="section.id"
      class="mb-10 scroll-mt-20"
    >
      <h2 class="font-semibold text-lg mb-3">{{ section.heading }}</h2>

      <template
        v-for="(block, i) in section.blocks"
        :key="i"
      >
        <p
          v-if="block.type === 'p'"
          class="text-neutral-600 dark:text-neutral-400 mb-3"
        >
          {{ block.text }}
        </p>

        <ul
          v-else-if="block.type === 'list'"
          class="list-disc pl-5 space-y-2 mb-3 text-neutral-600 dark:text-neutral-400"
        >
          <li
            v-for="(item, j) in block.items"
            :key="j"
          >
            {{ item }}
          </li>
        </ul>

        <template v-else-if="block.type === 'contact'">
          <i18n-t
            v-if="doc.contactEmail"
            keypath="legal.contactWithEmail"
            tag="p"
            class="text-neutral-600 dark:text-neutral-400 mb-3"
          >
            <template #email>
              <ULink :to="`mailto:${doc.contactEmail}`" class="text-primary-500 hover:underline">{{ doc.contactEmail }}</ULink>
            </template>
          </i18n-t>
          <i18n-t
            v-else
            keypath="legal.contactWithPage"
            tag="p"
            class="text-neutral-600 dark:text-neutral-400 mb-3"
          >
            <template #page>
              <ULink to="/contact" class="text-primary-500 hover:underline">{{ t('legal.contactPageLink') }}</ULink>
            </template>
          </i18n-t>
        </template>
      </template>
    </section>
  </UContainer>
</template>
