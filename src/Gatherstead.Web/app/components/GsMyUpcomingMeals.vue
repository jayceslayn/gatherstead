<script setup lang="ts">
import { useMyMeals } from '~/composables/useMyUpcoming'
import type { MyMeal } from '~/repositories/types'

const props = withDefaults(defineProps<{ limit?: number }>(), { limit: 5 })

const { t } = useI18n()
const { formatDate } = useFormatDate()
const { meals, pending } = useMyMeals()

const visible = computed(() => meals.value.slice(0, props.limit))

// Built in script so the "·" separator is not a raw-text node (no-raw-text lint rule).
const subtitle = (meal: MyMeal) =>
  `${formatDate(meal.day)} · ${t(`event.meal.${meal.mealType.toLowerCase()}`)}`
</script>

<template>
  <div>
    <h2 class="text-xs font-semibold text-muted uppercase tracking-wider mb-3">
      {{ t('dashboard.myMeals') }}
    </h2>

    <div v-if="pending" class="rounded-lg border border-(--ui-border) bg-elevated p-6 text-center">
      <p class="text-sm text-muted">{{ t('common.loading') }}</p>
    </div>

    <div
      v-else-if="!visible.length"
      class="rounded-lg border border-(--ui-border) bg-elevated p-6 flex flex-col items-center text-center gap-2"
    >
      <UIcon name="i-heroicons-cake" class="size-8 text-muted" />
      <p class="text-sm text-muted">{{ t('dashboard.noMeals') }}</p>
    </div>

    <ul v-else class="space-y-2">
      <li v-for="meal in visible" :key="meal.id">
        <NuxtLink
          :to="`/app/events/${meal.eventId}/meal-planner?plan=${meal.mealPlanId}`"
          class="rounded-lg border border-(--ui-border) bg-elevated p-3 flex items-center gap-3 hover:ring-1 hover:ring-primary transition-all"
        >
          <UIcon name="i-heroicons-cake" class="size-5 text-primary shrink-0" />
          <div class="min-w-0 flex-1">
            <p class="text-sm font-medium truncate">{{ meal.templateName }}</p>
            <p class="text-xs text-muted truncate">{{ subtitle(meal) }}</p>
          </div>
          <UBadge v-if="!meal.notes" color="warning" variant="subtle" size="sm" class="shrink-0">
            {{ t('mealPlanner.needsMenuPlan') }}
          </UBadge>
          <UIcon name="i-heroicons-chevron-right" class="size-4 text-muted shrink-0" />
        </NuxtLink>
      </li>
    </ul>
  </div>
</template>
