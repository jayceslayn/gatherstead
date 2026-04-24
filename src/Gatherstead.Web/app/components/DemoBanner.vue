<script setup lang="ts">
import { DEMO_LIMITS } from '~/repositories/demo/DemoStore'

const config = useRuntimeConfig()
const { t } = useI18n()
const open = ref(false)

type LimitKey = keyof typeof DEMO_LIMITS
const limitKeys = Object.keys(DEMO_LIMITS) as LimitKey[]
</script>

<template>
  <div
    v-if="config.public.demoMode"
    class="sticky top-0 z-50 bg-amber-50 dark:bg-amber-950 border-b border-amber-200 dark:border-amber-800"
  >
    <div class="max-w-screen-xl mx-auto px-4 py-1.5 flex items-center justify-between gap-4">
      <div class="flex items-center gap-2">
        <UIcon name="i-heroicons-beaker" class="size-4 text-amber-600 dark:text-amber-400 shrink-0" />
        <span class="text-sm font-medium text-amber-900 dark:text-amber-100">{{ t('demo.banner.title') }}</span>
      </div>
      <UButton size="xs" variant="ghost" color="warning" @click="open = true">
        {{ t('demo.banner.learnMore') }}
      </UButton>
    </div>
  </div>

  <UModal v-model:open="open">
    <template #content>
      <div class="p-6">
        <h3 class="text-lg font-semibold mb-2">{{ t('demo.modal.title') }}</h3>
        <p class="text-sm text-muted mb-4">{{ t('demo.modal.intro') }}</p>

        <h4 class="text-sm font-semibold mb-2">{{ t('demo.modal.limitsTitle') }}</h4>
        <table class="w-full text-sm mb-4">
          <tbody>
            <tr
              v-for="key in limitKeys"
              :key="key"
              class="border-b border-(--ui-border) last:border-0"
            >
              <td class="py-1.5 text-muted">{{ t(`demo.modal.limits.${key}`) }}</td>
              <td class="py-1.5 text-right font-medium">{{ DEMO_LIMITS[key] }}</td>
            </tr>
          </tbody>
        </table>

        <p class="text-xs text-muted mb-6">{{ t('demo.modal.dataResets') }}</p>

        <div class="flex items-center gap-3">
          <p v-if="config.public.liveUrl" class="text-sm text-muted flex-1">
            {{ t('demo.modal.cta') }}
          </p>
          <div class="flex gap-2 ml-auto">
            <UButton
              v-if="config.public.liveUrl"
              color="primary"
              :to="(config.public.liveUrl as string)"
              external
            >
              {{ t('demo.banner.goLive') }}
            </UButton>
            <UButton variant="ghost" @click="open = false">{{ t('common.cancel') }}</UButton>
          </div>
        </div>
      </div>
    </template>
  </UModal>
</template>
