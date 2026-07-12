<script setup lang="ts">
import { buildPrivacyDoc } from '~/content/legal/privacy'
import { resolveLegalContext } from '~/content/legal/types'

definePageMeta({
  layout: 'landing',
})

const { t } = useI18n()
const config = useRuntimeConfig()

const doc = computed(() =>
  buildPrivacyDoc(resolveLegalContext(config.public, t('common.appName'))),
)

useSeoMeta({
  title: () => `${doc.value.title} · ${t('common.appName')}`,
  description: () => t('legal.privacyDescription', { app: t('common.appName') }),
})
</script>

<template>
  <GsLegalDocument :doc="doc" />
</template>
