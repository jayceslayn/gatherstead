<script setup lang="ts">
import { useMyShopping } from '~/composables/useMyUpcoming'

const props = withDefaults(defineProps<{ limit?: number }>(), { limit: 5 })

const { t } = useI18n()
const { formatDate } = useFormatDate()
const { items, pending } = useMyShopping()

const visible = computed(() => items.value.slice(0, props.limit))
</script>

<template>
  <div>
    <h2 class="text-xs font-semibold text-muted uppercase tracking-wider mb-3">
      {{ t('dashboard.myShopping') }}
    </h2>

    <div v-if="pending" class="rounded-lg border border-(--ui-border) bg-elevated p-6 text-center">
      <p class="text-sm text-muted">{{ t('common.loading') }}</p>
    </div>

    <div
      v-else-if="!visible.length"
      class="rounded-lg border border-(--ui-border) bg-elevated p-6 flex flex-col items-center text-center gap-2"
    >
      <UIcon name="i-heroicons-shopping-cart" class="size-8 text-muted" />
      <p class="text-sm text-muted">{{ t('dashboard.noShopping') }}</p>
    </div>

    <ul v-else class="space-y-2">
      <li v-for="item in visible" :key="item.id">
        <NuxtLink
          to="/app/shopping"
          class="rounded-lg border border-(--ui-border) bg-elevated p-3 flex items-center gap-3 hover:ring-1 hover:ring-primary transition-all"
        >
          <UIcon name="i-heroicons-shopping-cart" class="size-5 text-primary shrink-0" />
          <div class="min-w-0 flex-1">
            <p class="text-sm font-medium truncate">{{ item.name }}</p>
            <p v-if="item.neededByDate" class="text-xs text-muted truncate">
              {{ t('shopping.neededBy') }} {{ formatDate(item.neededByDate) }}
            </p>
          </div>
          <UIcon name="i-heroicons-chevron-right" class="size-4 text-muted shrink-0" />
        </NuxtLink>
      </li>
    </ul>
  </div>
</template>
