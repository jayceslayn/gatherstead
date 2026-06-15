<script setup lang="ts">
import { inject } from 'vue'
import { REPOSITORIES_KEY } from '~/repositories/interfaces'
import type { Repositories } from '~/repositories/interfaces'
import { DEMO_LIMITS } from '~/repositories/demo/demoConstants'
import { useCurrentMemberStore } from '~/stores/member'
import { useEventStore } from '~/stores/event'

const config = useRuntimeConfig()
const isDemoMode = __DEMO_MODE__
const { t } = useI18n()
const open = ref(false)
const isResetting = ref(false)
const isClearing = ref(false)
const bannerRef = ref<HTMLElement | null>(null)

onMounted(() => {
  if (isDemoMode && bannerRef.value) {
    document.documentElement.style.setProperty('--gs-banner-h', `${bannerRef.value.offsetHeight}px`)
  }
})

onBeforeUnmount(() => {
  document.documentElement.style.removeProperty('--gs-banner-h')
})

const repos = inject<Repositories | null>(REPOSITORIES_KEY, null)
const memberStore = useCurrentMemberStore()
const eventStore = useEventStore()

type LimitKey = keyof typeof DEMO_LIMITS
const limitKeys = Object.keys(DEMO_LIMITS) as LimitKey[]

async function resetDemoData() {
  if (!__DEMO_MODE__ || !repos) return
  isResetting.value = true
  const { clearDemoStore } = await import('~/repositories/demo/DemoStore')
  const { seedDemoData } = await import('~/repositories/demo/seedDemoData')
  clearDemoStore()
  await seedDemoData(repos)
  memberStore.clear()
  eventStore.clear()
  clearNuxtData()
  open.value = false
  await navigateTo('/app')
  isResetting.value = false
}

async function clearDemoData() {
  if (!__DEMO_MODE__ || !repos) return
  isClearing.value = true
  const { clearDemoStore } = await import('~/repositories/demo/DemoStore')
  clearDemoStore()
  memberStore.clear()
  eventStore.clear()
  clearNuxtData()
  open.value = false
  await navigateTo('/app')
  isClearing.value = false
}
</script>

<template>
  <div
    v-if="isDemoMode"
    ref="bannerRef"
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

  <UModal v-model:open="open" :title="t('demo.modal.title')">
    <template #body>
      <p class="text-sm text-muted mb-4">{{ t('demo.modal.intro') }}</p>

      <GsCollapsible class="mb-4" button-class="text-sm font-medium">
        {{ t('demo.modal.limitsTitle') }}
        <template #content>
          <table class="w-full text-sm">
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
        </template>
      </GsCollapsible>

      <p class="text-sm text-muted mb-6">{{ t('demo.modal.dataResets') }}</p>

      <div class="flex flex-wrap justify-between gap-2">
        <UButton
          color="warning"
          :loading="isResetting"
          :disabled="isResetting || isClearing"
          @click="resetDemoData"
        >
          {{ isResetting ? t('demo.modal.resetting') : t('demo.modal.resetButton') }}
        </UButton>
        <UButton
          color="error"
          :loading="isClearing"
          :disabled="isResetting || isClearing"
          @click="clearDemoData"
        >
          {{ isClearing ? t('demo.modal.clearing') : t('demo.modal.clearButton') }}
        </UButton>
      </div>
    </template>

    <template #footer>
      <div class="flex justify-between items-center gap-3 w-full">
        <p v-if="config.public.liveUrl" class="text-sm text-muted flex-1">
          {{ t('demo.modal.cta') }}
        </p>
        <UButton
          v-if="config.public.liveUrl"
          color="primary"
          :to="(config.public.liveUrl as string)"
          external
        >
          {{ t('demo.banner.goLive') }}
        </UButton>
      </div>
    </template>
  </UModal>
</template>
