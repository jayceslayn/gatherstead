<script setup lang="ts">
import { buildTermsDoc } from '~/content/legal/terms'
import { resolveLegalContext } from '~/content/legal/types'

definePageMeta({
  layout: 'landing',
})

const { t } = useI18n()
const config = useRuntimeConfig()

const doc = computed(() =>
  buildTermsDoc(resolveLegalContext(config.public, t('common.appName'))),
)

useSeoMeta({
  title: () => `${doc.value.title} · ${t('common.appName')}`,
  description: () => t('legal.termsDescription', { app: t('common.appName') }),
})
</script>

<template>
  <GsLegalDocument :doc="doc" />
</template>
